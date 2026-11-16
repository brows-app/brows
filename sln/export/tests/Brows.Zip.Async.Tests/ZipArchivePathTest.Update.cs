using Brows.Entries;
using Brows.Operations;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Brows;

internal sealed partial class ZipArchivePathTest {
    private UpdateTestSandbox UpdateSandbox;

    [SetUp]
    public void SetUpUpdate() {
        UpdateSandbox = new();
    }

    [TearDown]
    public void TearDownUpdate() {
        UpdateSandbox.Dispose();
    }

    [Test]
    public async Task Update_MissingFilePreservesTheOriginalArchive() {
        var archiveFileName = CreateArchive(
            "missing-source.zip",
            ("important.txt", "important"),
            ("keep.txt", "keep"));
        var original = File.ReadAllBytes(archiveFileName);
        var path = OpenUpdateArchive(archiveFileName);
        try {
            await Assert.ThrowsAsync<FileNotFoundException>(() => path.Update(
                progress: null,
                token: CancellationToken.None,
                info: new() {
                    CreateEntriesFromFiles = new Dictionary<string, string> {
                        ["important.txt"] = UpdateSandbox.PathFor("absent.txt")
                    }
                }));
            Assert.That(File.ReadAllBytes(archiveFileName), Is.EqualTo(original));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_CancellationAfterADeletionPreservesTheOriginalArchive() {
        var archiveFileName = CreateArchive("cancel-delete.zip", ("first.txt", "first"), ("second.txt", "second"));
        var original = File.ReadAllBytes(archiveFileName);
        using var cancellation = new CancellationTokenSource();
        var progress = new CancelAfterOneItemProgress(cancellation);
        var path = OpenUpdateArchive(archiveFileName);
        try {
            await Assert.ThrowsAsync<OperationCanceledException>(() => path.Update(
                info: new() {
                    DeleteEntries = new HashSet<string> { "first.txt" }
                },
                progress: progress,
                token: cancellation.Token));
            Assert.That(File.ReadAllBytes(archiveFileName), Is.EqualTo(original));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_FailingStreamAfterPartialReadPreservesTheOriginalArchive() {
        var archiveFileName = CreateArchive("failing-stream.zip", ("keep.txt", "keep"));
        var original = File.ReadAllBytes(archiveFileName);
        var content = Enumerable.Range(0, 16384).Select(value => (byte)value).ToArray();
        var source = new TestEntryStreamSource("broken.bin", new FailingReadStream(content, 1024));
        var path = OpenUpdateArchive(archiveFileName);
        try {
            await Assert.ThrowsAsync<IOException>(() => path.Update(
                progress: null,
                token: CancellationToken.None,
                info: new() {
                    DeleteEntries = new HashSet<string> { "keep.txt" },
                    CopyStreamSets = new List<IEntryStreamSet> {
                        new TestEntryStreamSet(source)
                    }
                }));
            Assert.That(File.ReadAllBytes(archiveFileName), Is.EqualTo(original));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_FailingLaterStreamSetRollsBackEarlierStreamSet() {
        var archiveFileName = CreateArchive("stream-batch.zip", ("keep.txt", "keep"));
        var original = File.ReadAllBytes(archiveFileName);
        var first = new TestEntryStreamSource("first.txt", new MemoryStream(Encoding.UTF8.GetBytes("first")));
        var content = Enumerable.Range(0, 8192).Select(value => (byte)value).ToArray();
        var second = new TestEntryStreamSource("second.txt", new FailingReadStream(content, 512));
        var path = OpenUpdateArchive(archiveFileName);
        try {
            await Assert.ThrowsAsync<IOException>(() => path.Update(
                progress: null,
                token: CancellationToken.None,
                info: new() {
                    CopyStreamSets = new List<IEntryStreamSet> {
                        new TestEntryStreamSet(first),
                        new TestEntryStreamSet(second)
                    }
                }));
            Assert.That(File.ReadAllBytes(archiveFileName), Is.EqualTo(original));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_CommitsFileAndStreamChangesTogether() {
        var archiveFileName = CreateArchive("success.zip", ("old.txt", "old"), ("keep.txt", "keep"));
        var sourceFileName = UpdateSandbox.WriteFile("created-source.txt", "created");
        var source = new TestEntryStreamSource("copied.txt", new MemoryStream(Encoding.UTF8.GetBytes("copied")));
        var path = OpenUpdateArchive(archiveFileName);
        try {
            await path.Update(
                progress: null,
                token: CancellationToken.None,
                info: new() {
                    DeleteEntries = new HashSet<string> { "old.txt" },
                    CreateEntriesFromFiles = new Dictionary<string, string> {
                        ["created.txt"] = sourceFileName
                    },
                    CopyStreamSets = new List<IEntryStreamSet> {
                        new TestEntryStreamSet(source)
                    }
                });
            var entries = ReadEntries(archiveFileName);
            using (Assert.EnterMultipleScope()) {
                Assert.That(entries, Does.Not.ContainKey("old.txt"));
                Assert.That(entries["keep.txt"], Is.EqualTo("keep"));
                Assert.That(entries["created.txt"], Is.EqualTo("created"));
                Assert.That(entries["copied.txt"], Is.EqualTo("copied"));
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_PreservesUnownedSiblingTemporaryFiles() {
        var archiveFileName = CreateArchive("sibling.zip", ("old.txt", "old"));
        var userTemporaryFile = UpdateSandbox.PathFor(".brows-update-user.tmp");
        File.WriteAllText(userTemporaryFile, "user-owned", new UTF8Encoding(false));
        var path = OpenUpdateArchive(archiveFileName);
        try {
            await path.Update(
                progress: null,
                token: CancellationToken.None,
                info: new() {
                    DeleteEntries = new HashSet<string> { "old.txt" }
                });
            var siblingFiles = Directory.GetFiles(
                UpdateSandbox.Root,
                ".brows-update-*");
            using (Assert.EnterMultipleScope()) {
                Assert.That(File.ReadAllText(userTemporaryFile), Is.EqualTo("user-owned"));
                Assert.That(siblingFiles, Is.EquivalentTo(new[] { userTemporaryFile }));
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_CommitFailurePreservesTheOriginalAndCleansItsStage() {
        var archiveFileName = CreateArchive("commit-failure.zip", ("old.txt", "old"));
        var original = File.ReadAllBytes(archiveFileName);
        using var blocker = new FileStream(archiveFileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        var path = OpenUpdateArchive(archiveFileName);
        try {
            await Assert.ThrowsAsync<IOException>(() => path.Update(
                progress: null,
                token: CancellationToken.None,
                info: new() {
                    DeleteEntries = new HashSet<string> { "old.txt" }
                }));
            var stagedFiles = Directory.GetFiles(UpdateSandbox.Root, ".brows-update-*");
            using (Assert.EnterMultipleScope()) {
                Assert.That(File.ReadAllBytes(archiveFileName), Is.EqualTo(original));
                Assert.That(stagedFiles, Is.Empty);
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_NestedArchiveCommitsSuccessfully() {
        var archiveFileName = CreateNestedArchive("nested-success.zip", "inside.zip", ("old.txt", "old"));
        var sourceFileName = UpdateSandbox.WriteFile("nested-source.txt", "new");
        var path = OpenUpdateArchive(archiveFileName, "inside.zip");
        try {
            await path.Update(
                progress: null,
                token: CancellationToken.None,
                info: new() {
                    DeleteEntries = new HashSet<string> { "old.txt" },
                    CreateEntriesFromFiles = new Dictionary<string, string> {
                        ["new.txt"] = sourceFileName
                    }
                });
            var entries = ReadNestedEntries(archiveFileName, "inside.zip");
            using (Assert.EnterMultipleScope()) {
                Assert.That(entries, Does.Not.ContainKey("old.txt"));
                Assert.That(entries["new.txt"], Is.EqualTo("new"));
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_NestedFailurePreservesTheOuterArchiveByteForByte() {
        var archiveFileName = CreateNestedArchive("nested-failure.zip", "inside.zip", ("important.txt", "important"));
        var original = File.ReadAllBytes(archiveFileName);
        var path = OpenUpdateArchive(archiveFileName, "inside.zip");
        try {
            await Assert.ThrowsAsync<FileNotFoundException>(() => path.Update(
                progress: null,
                token: CancellationToken.None,
                info: new() {
                    DeleteEntries = new HashSet<string> { "important.txt" },
                    CreateEntriesFromFiles = new Dictionary<string, string> {
                        ["missing.txt"] = UpdateSandbox.PathFor("absent-nested.txt")
                    }
                }));
            Assert.That(File.ReadAllBytes(archiveFileName), Is.EqualTo(original));
        }
        finally {
            path.Release();
        }
    }

    private static ZipArchivePath OpenUpdateArchive(string archiveFileName, params string[] nesting) {
        return ZipArchivePath.Get(new FileInfo(archiveFileName), new ZipArchiveNest(nesting));
    }

    private string CreateArchive(string name, params (string Name, string Contents)[] entries) {
        var archiveFileName = UpdateSandbox.PathFor(name);
        using var file = new FileStream(archiveFileName, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);
        foreach (var item in entries) {
            var entry = archive.CreateEntry(item.Name);
            using var stream = entry.Open();
            var bytes = Encoding.UTF8.GetBytes(item.Contents);
            stream.Write(bytes);
        }
        return archiveFileName;
    }

    private string CreateNestedArchive(
        string name,
        string nestedName,
        params (string Name, string Contents)[] entries) {
        var nestedBytes = CreateArchiveBytes(entries);
        var archiveFileName = UpdateSandbox.PathFor(name);
        using var file = new FileStream(archiveFileName, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);
        var nested = archive.CreateEntry(nestedName);
        using (var stream = nested.Open()) {
            stream.Write(nestedBytes);
        }
        return archiveFileName;
    }

    private static byte[] CreateArchiveBytes((string Name, string Contents)[] entries) {
        using var file = new MemoryStream();
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true)) {
            foreach (var item in entries) {
                var entry = archive.CreateEntry(item.Name);
                using var stream = entry.Open();
                var bytes = Encoding.UTF8.GetBytes(item.Contents);
                stream.Write(bytes);
            }
        }
        return file.ToArray();
    }

    private static Dictionary<string, string> ReadEntries(string archiveFileName) {
        using var file = File.OpenRead(archiveFileName);
        using var archive = new ZipArchive(file, ZipArchiveMode.Read);
        return ReadEntries(archive);
    }

    private static Dictionary<string, string> ReadNestedEntries(string archiveFileName, string nestedName) {
        using var file = File.OpenRead(archiveFileName);
        using var archive = new ZipArchive(file, ZipArchiveMode.Read);
        var nested = archive.GetEntry(nestedName)
            ?? throw new InvalidOperationException("The nested archive entry was not found.");
        using var nestedStream = nested.Open();
        using var nestedFile = new MemoryStream();
        nestedStream.CopyTo(nestedFile);
        nestedFile.Position = 0;
        using var nestedArchive = new ZipArchive(nestedFile, ZipArchiveMode.Read);
        return ReadEntries(nestedArchive);
    }

    private static Dictionary<string, string> ReadEntries(ZipArchive archive) {
        var result = new Dictionary<string, string>();
        foreach (var entry in archive.Entries) {
            using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            result.Add(entry.FullName, reader.ReadToEnd());
        }
        return result;
    }

    private sealed class TestEntryStreamSet : IEntryStreamSet {
        private readonly IEntryStreamSource[] Sources;

        public TestEntryStreamSet(params IEntryStreamSource[] sources) {
            Sources = sources ?? throw new ArgumentNullException(nameof(sources));
        }

        public IEntryStreamReady StreamSourceReady() {
            return new TestEntryStreamReady();
        }

        public IEnumerable<string> FileSource() {
            return Array.Empty<string>();
        }

        public async IAsyncEnumerable<IEntryStreamSource> StreamSource(
            [EnumeratorCancellation] CancellationToken token) {
            foreach (var source in Sources) {
                token.ThrowIfCancellationRequested();
                yield return source;
            }
            await Task.CompletedTask;
        }
    }

    private sealed class TestEntryStreamSource : IEntryStreamSource {
        private readonly long Length;

        public string EntryID => RelativePath;
        public string EntryName => RelativePath;
        public string RelativePath { get; }
        public string SourceFile => null;
        public string SourceDirectory => null;
        public long StreamLength => Length;
        public Stream Stream { get; }

        public TestEntryStreamSource(string relativePath, Stream stream, long? streamLength = null) {
            RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
            Stream = stream;
            Length = streamLength ?? (stream?.CanSeek == true ? stream.Length : 0);
        }

        public Task<IEntryStreamReady> StreamReady(CancellationToken token) {
            token.ThrowIfCancellationRequested();
            return Task.FromResult<IEntryStreamReady>(new TestEntryStreamReady());
        }
    }

    private sealed class TestEntryStreamReady : IEntryStreamReady {
        public void Dispose() {
        }
    }

    private sealed class FailingReadStream : MemoryStream {
        private readonly int FailAfter;

        private int BytesRead;

        public FailingReadStream(byte[] content, int failAfter) : base(content) {
            FailAfter = failAfter;
        }

        public override int Read(byte[] buffer, int offset, int count) {
            if (BytesRead >= FailAfter) {
                throw new IOException("Injected stream read failure.");
            }
            var allowed = Math.Min(count, FailAfter - BytesRead);
            var read = base.Read(buffer, offset, allowed);
            BytesRead += read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) {
            if (BytesRead >= FailAfter) {
                throw new IOException("Injected stream read failure.");
            }
            var allowed = Math.Min(buffer.Length, FailAfter - BytesRead);
            var read = await base.ReadAsync(buffer[..allowed], cancellationToken);
            BytesRead += read;
            return read;
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) {
            return ReadArrayAsync(buffer, offset, count, cancellationToken);
        }

        private async Task<int> ReadArrayAsync(byte[] buffer, int offset, int count, CancellationToken token) {
            var read = await ReadAsync(buffer.AsMemory(offset, count), token);
            return read;
        }
    }

    private sealed class CancelAfterOneItemProgress : IOperationProgress {
        private readonly CancellationTokenSource Cancellation;

        private bool Cancelled;

        public CancelAfterOneItemProgress(CancellationTokenSource cancellation) {
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
            var hasFirstCompletedItem = addProgress.GetValueOrDefault() > 0 && Cancelled == false;
            if (hasFirstCompletedItem) {
                Cancelled = true;
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
}
