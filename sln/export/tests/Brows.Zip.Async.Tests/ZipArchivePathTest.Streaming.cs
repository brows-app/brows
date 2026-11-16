using Brows.Entries;
using Brows.Operations;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed partial class ZipArchivePathTest {
    private string CreateArchiveWithPayload(string name, string entryName, byte[] contents) {
        var archiveFileName = UpdateSandbox.PathFor(name);
        using var file = new FileStream(archiveFileName, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);
        var entry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
        using (var stream = entry.Open()) {
            stream.Write(contents);
        }
        return archiveFileName;
    }

    [Test]
    public async Task Update_ImportsDistinctWhitespaceNamesWithoutReplacingTheTrimmedName() {
        var archiveFileName = CreateArchive("whitespace-names.zip", ("report.txt", "keep this file"));
        var source = new TestEntryStreamSource(
            " report.txt ",
            new MemoryStream(Encoding.UTF8.GetBytes("whitespace member")));
        var path = OpenUpdateArchive(archiveFileName);
        try {
            await path.Update(
                info: new() {
                    CopyStreamSets = new List<IEntryStreamSet> {
                        new TestEntryStreamSet(source)
                    }
                },
                progress: null,
                token: CancellationToken.None);

            var entries = ReadEntries(archiveFileName);
            using (Assert.EnterMultipleScope()) {
                Assert.That(entries["report.txt"], Is.EqualTo("keep this file"));
                Assert.That(entries[" report.txt "], Is.EqualTo("whitespace member"));
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    [NonParallelizable]
    public async Task Update_LargeReportedLengthUsesBoundedMemoryAndWritesTheCompleteStream() {
        const int contentLength = 32 * 1024 * 1024;
        var reportedLength = (long)int.MaxValue + 1024;
        var archiveFileName = CreateArchive("large-stream.zip", ("keep.txt", "keep"));
        var sourceStream = new GeneratedReadStream(contentLength, 0x5a);
        var source = new TestEntryStreamSource("large.bin", sourceStream, reportedLength);
        var path = OpenUpdateArchive(archiveFileName);
        try {
            var allocationStart = GC.GetTotalAllocatedBytes(precise: true);
            await path.Update(
                info: new() {
                    CopyStreamSets = new List<IEntryStreamSet> {
                        new TestEntryStreamSet(source)
                    }
                },
                progress: null,
                token: CancellationToken.None);
            var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocationStart;

            using var file = File.OpenRead(archiveFileName);
            using var archive = new ZipArchive(file, ZipArchiveMode.Read);
            var entry = archive.GetEntry("large.bin");
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.Length, Is.EqualTo(contentLength));
            await using var input = await entry.OpenAsync(CancellationToken.None);
            var buffer = new byte[81920];
            long copied = 0;
            for (; ; ) {
                var read = await input.ReadAsync(buffer.AsMemory(), CancellationToken.None);
                if (read == 0) {
                    break;
                }
                var isExpectedContent = true;
                for (var index = 0; index < read; index++) {
                    if (buffer[index] != 0x5a) {
                        isExpectedContent = false;
                        break;
                    }
                }
                Assert.That(isExpectedContent, Is.True);
                copied += read;
            }
            using (Assert.EnterMultipleScope()) {
                Assert.That(copied, Is.EqualTo(contentLength));
                Assert.That(sourceStream.BytesRead, Is.EqualTo(contentLength));
                Assert.That(allocated, Is.LessThan(16L * 1024 * 1024));
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_CancellationAfterStreamBytesAreWrittenPreservesTheArchive() {
        var archiveFileName = CreateArchive("cancel-stream-copy.zip", ("keep.txt", "keep"));
        var original = File.ReadAllBytes(archiveFileName);
        using var cancellation = new CancellationTokenSource();
        var progress = new ByteProgress();
        var sourceStream = new BlockAfterFirstReadStream(128 * 1024);
        var source = new TestEntryStreamSource("blocked.bin", sourceStream, 128 * 1024);
        var path = OpenUpdateArchive(archiveFileName);
        try {
            var update = path.Update(
                info: new() {
                    CopyStreamSets = new List<IEntryStreamSet> {
                        new TestEntryStreamSet(source)
                    }
                },
                progress: progress,
                token: cancellation.Token);

            await progress.FirstBytesCopied.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            await Assert.CatchAsync<OperationCanceledException>(() => update);

            using (Assert.EnterMultipleScope()) {
                Assert.That(progress.BytesCopied, Is.GreaterThan(0));
                Assert.That(File.ReadAllBytes(archiveFileName), Is.EqualTo(original));
                Assert.That(Directory.GetFiles(UpdateSandbox.Root, ".brows-update-*"), Is.Empty);
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_CancellationDuringArchiveRewritePreservesTheArchive() {
        var archiveFileName = CreateArchiveWithPayload(
            "cancel-rewrite.zip",
            "keep.bin",
            new byte[4 * 1024 * 1024]);
        var original = File.ReadAllBytes(archiveFileName);
        using var cancellation = new CancellationTokenSource();
        var progress = new CancelOnEntryBytesProgress("keep.bin", cancellation);
        var path = OpenUpdateArchive(archiveFileName);
        try {
            var update = path.Update(
                info: new() {
                    DeleteEntries = new HashSet<string> { "missing.txt" }
                },
                progress: progress,
                token: cancellation.Token);

            await progress.FirstBytesCopied.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.CatchAsync<OperationCanceledException>(() => update);

            using (Assert.EnterMultipleScope()) {
                Assert.That(progress.BytesCopied, Is.GreaterThan(0));
                Assert.That(File.ReadAllBytes(archiveFileName), Is.EqualTo(original));
                Assert.That(Directory.GetFiles(UpdateSandbox.Root, ".brows-update-*"), Is.Empty);
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Read_CancellationAfterBytesAreWrittenPreservesTheDestination() {
        var archiveFileName = CreateArchiveWithPayload(
            "cancel-read.zip",
            "payload.bin",
            new byte[4 * 1024 * 1024]);
        var destination = UpdateSandbox.WriteFile("output/payload.bin", "keep destination");
        using var cancellation = new CancellationTokenSource();
        var progress = new CancelOnEntryBytesProgress("payload.bin", cancellation);
        var path = OpenUpdateArchive(archiveFileName);
        try {
            var read = path.Read(
                new ZipArchiveRead {
                    ExtractOverwrites = true,
                    ExtractEntriesToFiles = new Dictionary<string, string> {
                        ["payload.bin"] = destination
                    }
                },
                progress,
                cancellation.Token);

            await Assert.CatchAsync<OperationCanceledException>(() => read);

            using (Assert.EnterMultipleScope()) {
                Assert.That(progress.BytesCopied, Is.GreaterThan(0));
                Assert.That(File.ReadAllText(destination), Is.EqualTo("keep destination"));
                Assert.That(Directory.GetFiles(Path.GetDirectoryName(destination)!, ".brows-*.tmp"), Is.Empty);
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Read_NestedArchiveStagesToDiskAndExtractsTheSelectedEntry() {
        var expected = Encoding.UTF8.GetBytes("nested archive content");
        var nested = CreateArchive("payload.bin", expected, CompressionLevel.NoCompression);
        var outer = CreateArchive("inside.zip", nested, CompressionLevel.NoCompression);
        var archiveFileName = UpdateSandbox.PathFor("nested-read.zip");
        File.WriteAllBytes(archiveFileName, outer);
        var destination = UpdateSandbox.WriteFile("output/payload.bin", "keep destination");
        var progress = new ByteProgress();
        var path = ZipArchivePath.Get(
            new FileInfo(archiveFileName),
            new ZipArchiveNest(new[] { "inside.zip" }));
        try {
            await path.Read(
                new ZipArchiveRead {
                    ExtractOverwrites = true,
                    ExtractEntriesToFiles = new Dictionary<string, string> {
                        ["payload.bin"] = destination
                    }
                },
                progress,
                token: CancellationToken.None);

            using (Assert.EnterMultipleScope()) {
                Assert.That(File.ReadAllBytes(destination), Is.EqualTo(expected));
                Assert.That(progress.BytesCopied, Is.EqualTo(nested.Length + expected.Length));
                Assert.That(progress.BytesTarget, Is.EqualTo(progress.BytesCopied));
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_NestedRewriteDeniesExternalWritesAndPreservesTheOriginalOnCancel() {
        var payload = new byte[4 * 1024 * 1024];
        var nested = CreateArchive("payload.bin", payload, CompressionLevel.NoCompression);
        var outer = CreateArchive("inside.zip", nested, CompressionLevel.NoCompression);
        var archiveFileName = UpdateSandbox.PathFor("nested-write-lock.zip");
        File.WriteAllBytes(archiveFileName, outer);
        var original = File.ReadAllBytes(archiveFileName);
        using var cancellation = new CancellationTokenSource();
        var progress = new DenyArchiveWriteProgress(archiveFileName, "payload.bin", cancellation);
        var path = OpenUpdateArchive(archiveFileName, "inside.zip");
        try {
            var update = path.Update(
                info: new() {
                    DeleteEntries = new HashSet<string> { "missing.txt" }
                },
                progress: progress,
                token: cancellation.Token);

            await progress.WriteAttempted.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.CatchAsync<OperationCanceledException>(() => update);

            using (Assert.EnterMultipleScope()) {
                Assert.That(progress.BytesCopied, Is.GreaterThan(0));
                Assert.That(progress.WriteDenied, Is.True);
                Assert.That(File.ReadAllBytes(archiveFileName), Is.EqualTo(original));
                Assert.That(Directory.GetFiles(UpdateSandbox.Root, ".brows-update-*"), Is.Empty);
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_RewritePreservesArchiveAndEntryCommentsAndMetadata() {
        var archiveFileName = UpdateSandbox.PathFor("comments.zip");
        var lastWriteTime = new DateTimeOffset(2024, 5, 6, 7, 8, 10, TimeSpan.Zero);
        using (var file = new FileStream(archiveFileName, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None)) {
            using var archive = new ZipArchive(file, ZipArchiveMode.Create);
            archive.Comment = "archive comment";
            var keep = archive.CreateEntry("keep.txt");
            keep.Comment = "entry comment";
            keep.ExternalAttributes = (int)FileAttributes.ReadOnly;
            keep.LastWriteTime = lastWriteTime;
            using (var stream = keep.Open()) {
                stream.Write(Encoding.UTF8.GetBytes("keep"));
            }
            var remove = archive.CreateEntry("remove.txt");
            using var removeStream = remove.Open();
            removeStream.Write(Encoding.UTF8.GetBytes("remove"));
        }

        DateTimeOffset expectedLastWriteTime;
        using (var file = File.OpenRead(archiveFileName)) {
            using var archive = new ZipArchive(file, ZipArchiveMode.Read);
            expectedLastWriteTime = archive.GetEntry("keep.txt")!.LastWriteTime.ToUniversalTime();
        }

        var path = OpenUpdateArchive(archiveFileName);
        try {
            await path.Update(
                info: new() {
                    DeleteEntries = new HashSet<string> { "remove.txt" }
                },
                progress: null,
                token: CancellationToken.None);

            using var file = File.OpenRead(archiveFileName);
            using var archive = new ZipArchive(file, ZipArchiveMode.Read);
            var entry = archive.GetEntry("keep.txt");
            Assert.That(entry, Is.Not.Null);
            using (Assert.EnterMultipleScope()) {
                Assert.That(archive.Comment, Is.EqualTo("archive comment"));
                Assert.That(entry!.Comment, Is.EqualTo("entry comment"));
                Assert.That(entry.ExternalAttributes, Is.EqualTo((int)FileAttributes.ReadOnly));
                Assert.That(entry.LastWriteTime.ToUniversalTime(), Is.EqualTo(expectedLastWriteTime));
            }
        }
        finally {
            path.Release();
        }
    }

    private sealed class ByteProgress : IOperationProgress {
        private readonly TaskCompletionSource<bool> FirstBytes = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public long BytesCopied { get; private set; }

        public long BytesTarget { get; private set; }

        public Task FirstBytesCopied => FirstBytes.Task;

        public void Change(long? addProgress = null,
                           long? setProgress = null,
                           long? addTarget = null,
                           long? setTarget = null,
                           string progressString = null,
                           string targetString = null,
                           string name = null,
                           string data = null,
                           OperationProgressKind? kind = null,
                           string progressFormat = null,
                           string targetFormat = null,
                           string progressRateFormat = null,
                           string progressRateAverageFormat = null,
                           System.IFormatProvider formatProvider = null) {
            BytesCopied += addProgress.GetValueOrDefault();
            BytesTarget += addTarget.GetValueOrDefault();
            if (addProgress.GetValueOrDefault() > 0) {
                FirstBytes.TrySetResult(true);
            }
        }

        public async Task<OperationResult> Child(
            string name,
            OperationDelegate task,
            OperationFailureBehavior failureBehavior,
            CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                await task(this, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return new OperationResult(OperationResultKind.Succeeded, null);
            }
            catch (Exception error) {
                cancellationToken.ThrowIfCancellationRequested();
                if (failureBehavior == OperationFailureBehavior.Throw) {
                    throw;
                }
                var kind = error is OperationCanceledException ? OperationResultKind.Canceled : OperationResultKind.Failed;
                return new OperationResult(kind, error);
            }
        }

        public Task<OperationBatchResult> Children<T>(
            IEnumerable<T> source,
            Func<T, OperationChild> child,
            OperationFailureBehavior failureBehavior,
            CancellationToken cancellationToken) {
            throw new NotSupportedException();
        }
    }

    private sealed class CancelOnEntryBytesProgress : IOperationProgress {
        private readonly CancellationTokenSource Cancellation;
        private readonly string EntryName;
        private readonly TaskCompletionSource<bool> FirstBytes = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        private bool IsEntryCurrent;

        public long BytesCopied { get; private set; }

        public Task FirstBytesCopied => FirstBytes.Task;

        public CancelOnEntryBytesProgress(string entryName, CancellationTokenSource cancellation) {
            EntryName = entryName ?? throw new ArgumentNullException(nameof(entryName));
            Cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
        }

        public void Change(long? addProgress = null,
                           long? setProgress = null,
                           long? addTarget = null,
                           long? setTarget = null,
                           string progressString = null,
                           string targetString = null,
                           string name = null,
                           string data = null,
                           OperationProgressKind? kind = null,
                           string progressFormat = null,
                           string targetFormat = null,
                           string progressRateFormat = null,
                           string progressRateAverageFormat = null,
                           System.IFormatProvider formatProvider = null) {
            if (data == EntryName) {
                IsEntryCurrent = true;
            }
            var added = addProgress.GetValueOrDefault();
            if (IsEntryCurrent && added > 0) {
                BytesCopied += added;
                FirstBytes.TrySetResult(true);
                Cancellation.Cancel();
            }
        }

        public async Task<OperationResult> Child(
            string name,
            OperationDelegate task,
            OperationFailureBehavior failureBehavior,
            CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                await task(this, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return new OperationResult(OperationResultKind.Succeeded, null);
            }
            catch (Exception error) {
                cancellationToken.ThrowIfCancellationRequested();
                if (failureBehavior == OperationFailureBehavior.Throw) {
                    throw;
                }
                var kind = error is OperationCanceledException ? OperationResultKind.Canceled : OperationResultKind.Failed;
                return new OperationResult(kind, error);
            }
        }

        public Task<OperationBatchResult> Children<T>(
            IEnumerable<T> source,
            Func<T, OperationChild> child,
            OperationFailureBehavior failureBehavior,
            CancellationToken cancellationToken) {
            throw new NotSupportedException();
        }
    }

    private sealed class DenyArchiveWriteProgress : IOperationProgress {
        private readonly CancellationTokenSource Cancellation;
        private readonly string ArchiveFileName;
        private readonly string EntryName;
        private readonly TaskCompletionSource<bool> AttemptedWrite = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        private bool IsEntryCurrent;

        public long BytesCopied { get; private set; }

        public bool WriteDenied { get; private set; }

        public Task WriteAttempted => AttemptedWrite.Task;

        public DenyArchiveWriteProgress(
            string archiveFileName,
            string entryName,
            CancellationTokenSource cancellation) {
            ArchiveFileName = archiveFileName ?? throw new ArgumentNullException(nameof(archiveFileName));
            EntryName = entryName ?? throw new ArgumentNullException(nameof(entryName));
            Cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
        }

        public void Change(long? addProgress = null,
                           long? setProgress = null,
                           long? addTarget = null,
                           long? setTarget = null,
                           string progressString = null,
                           string targetString = null,
                           string name = null,
                           string data = null,
                           OperationProgressKind? kind = null,
                           string progressFormat = null,
                           string targetFormat = null,
                           string progressRateFormat = null,
                           string progressRateAverageFormat = null,
                           System.IFormatProvider formatProvider = null) {
            if (data == EntryName) {
                IsEntryCurrent = true;
            }
            var added = addProgress.GetValueOrDefault();
            var shouldAttemptWrite = IsEntryCurrent && added > 0 && AttemptedWrite.Task.IsCompleted is false;
            if (shouldAttemptWrite) {
                BytesCopied += added;
                try {
                    using var stream = new FileStream(
                        ArchiveFileName,
                        FileMode.Open,
                        FileAccess.Write,
                        FileShare.None);
                }
                catch (IOException) {
                    WriteDenied = true;
                }
                finally {
                    AttemptedWrite.TrySetResult(true);
                    Cancellation.Cancel();
                }
            }
        }

        public async Task<OperationResult> Child(
            string name,
            OperationDelegate task,
            OperationFailureBehavior failureBehavior,
            CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                await task(this, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return new OperationResult(OperationResultKind.Succeeded, null);
            }
            catch (Exception error) {
                cancellationToken.ThrowIfCancellationRequested();
                if (failureBehavior == OperationFailureBehavior.Throw) {
                    throw;
                }
                var kind = error is OperationCanceledException ? OperationResultKind.Canceled : OperationResultKind.Failed;
                return new OperationResult(kind, error);
            }
        }

        public Task<OperationBatchResult> Children<T>(
            IEnumerable<T> source,
            Func<T, OperationChild> child,
            OperationFailureBehavior failureBehavior,
            CancellationToken cancellationToken) {
            throw new NotSupportedException();
        }
    }

    private sealed class GeneratedReadStream : Stream {
        private readonly long TotalLength;
        private readonly byte Value;

        private long BytesPosition;

        public long BytesRead => BytesPosition;

        public GeneratedReadStream(long length, byte value) {
            TotalLength = length;
            Value = value;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) {
            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer) {
            var read = checked((int)Math.Min(buffer.Length, TotalLength - BytesPosition));
            buffer[..read].Fill(Value);
            BytesPosition += read;
            return read;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Read(buffer.Span));
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Read(buffer.AsSpan(offset, count)));
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) {
            throw new NotSupportedException();
        }
    }

    private sealed class BlockAfterFirstReadStream : Stream {
        private readonly long MaximumBytes;

        private bool HasRead;
        private long BytesPosition;

        public BlockAfterFirstReadStream(long maximumBytes) {
            MaximumBytes = maximumBytes;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) {
            throw new NotSupportedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
            return ReadAsyncCore(buffer, cancellationToken);
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) {
            return ReadAsyncCore(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        private async ValueTask<int> ReadAsyncCore(Memory<byte> buffer, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            if (HasRead) {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            HasRead = true;
            var read = checked((int)Math.Min(buffer.Length, MaximumBytes - BytesPosition));
            buffer.Span[..read].Fill(0x42);
            BytesPosition += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) {
            throw new NotSupportedException();
        }
    }
}
