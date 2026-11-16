using Brows.Operations;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brows.Commands;

[TestFixture]
internal sealed class ZipExtractorTest {
    private ZipCommandTestSandbox Sandbox;

    [SetUp]
    public void SetUp() {
        Sandbox = new();
    }

    [TearDown]
    public void TearDown() {
        Sandbox.Dispose();
    }

    private static byte[] CreateArchive((string Name, byte[] Contents, CompressionLevel Level)[] entries) {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true)) {
            foreach (var item in entries) {
                var entry = archive.CreateEntry(item.Name, item.Level);
                using var entryStream = entry.Open();
                entryStream.Write(item.Contents);
            }
        }
        return stream.ToArray();
    }

    private static int LocalDataOffset(byte[] archive) {
        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(archive.AsSpan(26, 2));
        var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(archive.AsSpan(28, 2));
        return 30 + nameLength + extraLength;
    }

    private static int LocalDataOffset(byte[] archive, int entryIndex) {
        var centralOffset = CentralHeaderOffset(archive);
        var centralEntryOffset = centralOffset;
        for (var index = 0; index < entryIndex; index++) {
            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(archive.AsSpan(centralEntryOffset + 28, 2));
            var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(archive.AsSpan(centralEntryOffset + 30, 2));
            var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(archive.AsSpan(centralEntryOffset + 32, 2));
            centralEntryOffset += 46 + nameLength + extraLength + commentLength;
        }

        var localHeaderOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(
            archive.AsSpan(centralEntryOffset + 42, 4)));
        var localNameLength = BinaryPrimitives.ReadUInt16LittleEndian(archive.AsSpan(localHeaderOffset + 26, 2));
        var localExtraLength = BinaryPrimitives.ReadUInt16LittleEndian(archive.AsSpan(localHeaderOffset + 28, 2));
        return localHeaderOffset + 30 + localNameLength + localExtraLength;
    }

    private static int CentralHeaderOffset(byte[] archive) {
        for (var offset = archive.Length - 22; offset >= 0; offset--) {
            var signature = BinaryPrimitives.ReadUInt32LittleEndian(archive.AsSpan(offset, 4));
            if (signature == 0x06054b50) {
                return checked((int)BinaryPrimitives.ReadUInt32LittleEndian(archive.AsSpan(offset + 16, 4)));
            }
        }
        throw new InvalidOperationException("The archive has no end-of-central-directory record.");
    }

    private static byte[] ReadArchiveEntry(byte[] archiveBytes) {
        using var stream = new MemoryStream(archiveBytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using var input = archive.Entries.Single().Open();
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] FindDeflateCrcMutation(byte[] archive, byte[] expected) {
        var dataOffset = LocalDataOffset(archive);
        var compressedLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(archive.AsSpan(18, 4)));
        for (var offset = dataOffset; offset < dataOffset + compressedLength; offset++) {
            for (var bit = 1; bit <= 0x80; bit <<= 1) {
                var candidate = (byte[])archive.Clone();
                candidate[offset] ^= (byte)bit;
                try {
                    var actual = ReadArchiveEntry(candidate);
                    if (actual.AsSpan().SequenceEqual(expected) == false) {
                        return candidate;
                    }
                }
                catch (InvalidDataException) {
                }
            }
        }
        throw new InvalidOperationException("No deflate CRC mutation could be produced.");
    }

    private static byte[] FindInvalidDeflateMutation(byte[] archive) {
        var dataOffset = LocalDataOffset(archive);
        var compressedLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(archive.AsSpan(18, 4)));
        for (var offset = dataOffset; offset < dataOffset + compressedLength; offset++) {
            for (var bit = 1; bit <= 0x80; bit <<= 1) {
                var candidate = (byte[])archive.Clone();
                candidate[offset] ^= (byte)bit;
                try {
                    using var stream = new MemoryStream(candidate, writable: false);
                    using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
                    using var entry = zip.Entries.Single().Open();
                    entry.CopyTo(Stream.Null);
                }
                catch (InvalidDataException) {
                    return candidate;
                }
            }
        }
        throw new InvalidOperationException("No invalid deflate mutation could be produced.");
    }

    [Test]
    public async Task Extract_RejectsStoredCrcCorruptionAndPreservesAnExistingOutput() {
        var contents = Encoding.UTF8.GetBytes("UNIQUE ZIP COMMAND CRC PAYLOAD");
        var archiveBytes = CreateArchive(new[] {
            ("payload.bin", contents, CompressionLevel.NoCompression)
        });
        archiveBytes[LocalDataOffset(archiveBytes)] ^= 1;
        var archiveFileName = Sandbox.PathFor("corrupt.zip");
        File.WriteAllBytes(archiveFileName, archiveBytes);
        var outputDirectory = Sandbox.CreateDirectory("output");
        var destination = Sandbox.WriteFile("output/payload.bin", "existing output");

        await Assert.ThrowsAsync<InvalidDataException>(() => ZipExtractor.ExtractAsync(
            archiveFileName,
            outputDirectory,
            overwrite: true,
            progress: null,
            token: CancellationToken.None));

        using (Assert.EnterMultipleScope()) {
            Assert.That(File.ReadAllText(destination), Is.EqualTo("existing output"));
            Assert.That(Directory.EnumerateFiles(outputDirectory, ".brows-*.tmp"), Is.Empty);
        }
    }

    [Test]
    public async Task Extract_RejectsDeflatedCrcCorruption() {
        var contents = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("deflated command payload ", 48)));
        var archiveBytes = CreateArchive(new[] {
            ("payload.bin", contents, CompressionLevel.Optimal)
        });
        var corrupted = FindDeflateCrcMutation(archiveBytes, contents);
        var archiveFileName = Sandbox.PathFor("corrupt-deflate.zip");
        File.WriteAllBytes(archiveFileName, corrupted);
        var outputDirectory = Sandbox.CreateDirectory("deflate-output");

        await Assert.ThrowsAsync<InvalidDataException>(() => ZipExtractor.ExtractAsync(
            archiveFileName,
            outputDirectory,
            overwrite: false,
            progress: null,
            token: CancellationToken.None));

        Assert.That(File.Exists(Path.Combine(outputDirectory, "payload.bin")), Is.False);
    }

    [Test]
    public async Task Extract_RejectsInvalidDeflateAndPreservesAnExistingOutput() {
        var contents = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("invalid deflate payload ", 64)));
        var archiveBytes = CreateArchive(new[] {
            ("payload.bin", contents, CompressionLevel.Optimal)
        });
        var corrupted = FindInvalidDeflateMutation(archiveBytes);
        var archiveFileName = Sandbox.PathFor("invalid-deflate.zip");
        File.WriteAllBytes(archiveFileName, corrupted);
        var outputDirectory = Sandbox.CreateDirectory("output");
        var destination = Sandbox.WriteFile("output/payload.bin", "preserve command output");

        await Assert.ThrowsAsync<InvalidDataException>(() => ZipExtractor.ExtractAsync(
            archiveFileName,
            outputDirectory,
            overwrite: true,
            progress: null,
            token: CancellationToken.None));

        using (Assert.EnterMultipleScope()) {
            Assert.That(File.ReadAllText(destination), Is.EqualTo("preserve command output"));
            Assert.That(Directory.EnumerateFiles(outputDirectory, ".brows-*.tmp"), Is.Empty);
        }
    }

    [Test]
    public async Task Extract_RejectsParentRelativeNamesBeforeWritingOutsideTheDestination() {
        var archiveFileName = Sandbox.PathFor("unsafe.zip");
        File.WriteAllBytes(archiveFileName, CreateArchive(new[] {
            ("../outside.txt", Encoding.UTF8.GetBytes("outside"), CompressionLevel.NoCompression)
        }));
        var outputDirectory = Sandbox.PathFor("output");
        var outside = Sandbox.PathFor("outside.txt");

        await Assert.ThrowsAsync<InvalidDataException>(() => ZipExtractor.ExtractAsync(
            archiveFileName,
            outputDirectory,
            overwrite: true,
            progress: null,
            token: CancellationToken.None));

        using (Assert.EnterMultipleScope()) {
            Assert.That(File.Exists(outside), Is.False);
            Assert.That(Directory.Exists(outputDirectory), Is.False);
        }
    }

    [Test]
    public async Task Extract_CreatesExplicitEmptyDirectories() {
        var archiveFileName = Sandbox.PathFor("empty-directory.zip");
        using (var stream = new FileStream(archiveFileName, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None)) {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
            archive.CreateEntry("empty/");
        }
        var outputDirectory = Sandbox.PathFor("output");

        await ZipExtractor.ExtractAsync(
            archiveFileName,
            outputDirectory,
            overwrite: false,
            progress: null,
            token: CancellationToken.None);

        Assert.That(Directory.Exists(Path.Combine(outputDirectory, "empty")), Is.True);
    }

    [Test]
    public async Task Extract_CancellationDuringEntryCopyPreservesTheExistingOutput() {
        var contents = new byte[4 * 1024 * 1024];
        var archiveFileName = Sandbox.PathFor("cancel-copy.zip");
        File.WriteAllBytes(archiveFileName, CreateArchive(new[] {
            ("payload.bin", contents, CompressionLevel.NoCompression)
        }));
        var outputDirectory = Sandbox.CreateDirectory("cancel-copy-output");
        var destination = Sandbox.WriteFile("cancel-copy-output/payload.bin", "preserve this output");
        using var cancellation = new CancellationTokenSource();
        var progress = new CancelOnFirstBytesProgress(cancellation);

        await Assert.CatchAsync<OperationCanceledException>(() => ZipExtractor.ExtractAsync(
            archiveFileName,
            outputDirectory,
            overwrite: true,
            progress,
            cancellation.Token));

        using (Assert.EnterMultipleScope()) {
            Assert.That(progress.BytesCopied, Is.GreaterThan(0));
            Assert.That(progress.BytesTarget, Is.EqualTo(contents.Length));
            Assert.That(File.ReadAllText(destination), Is.EqualTo("preserve this output"));
            Assert.That(Directory.EnumerateFiles(outputDirectory, ".brows-*.tmp"), Is.Empty);
        }
    }

    [Test]
    public async Task Extract_ProcessesDuplicateNamesInArchiveOrderWhenOverwriteIsEnabled() {
        var archiveFileName = Sandbox.PathFor("duplicate-valid.zip");
        File.WriteAllBytes(archiveFileName, CreateArchive(new[] {
            ("payload.bin", Encoding.UTF8.GetBytes("first"), CompressionLevel.NoCompression),
            ("payload.bin", Encoding.UTF8.GetBytes("second"), CompressionLevel.NoCompression)
        }));
        var outputDirectory = Sandbox.CreateDirectory("duplicate-output");

        await ZipExtractor.ExtractAsync(
            archiveFileName,
            outputDirectory,
            overwrite: true,
            progress: null,
            token: CancellationToken.None);

        Assert.That(File.ReadAllText(Path.Combine(outputDirectory, "payload.bin")), Is.EqualTo("second"));
    }

    [Test]
    public async Task Extract_RejectsDuplicateNamesWhenOverwriteIsDisabledAndKeepsTheFirstOutput() {
        var archiveFileName = Sandbox.PathFor("duplicate-no-overwrite.zip");
        File.WriteAllBytes(archiveFileName, CreateArchive(new[] {
            ("payload.bin", Encoding.UTF8.GetBytes("first"), CompressionLevel.NoCompression),
            ("payload.bin", Encoding.UTF8.GetBytes("second"), CompressionLevel.NoCompression)
        }));
        var outputDirectory = Sandbox.CreateDirectory("duplicate-no-overwrite-output");

        await Assert.ThrowsAsync<IOException>(() => ZipExtractor.ExtractAsync(
            archiveFileName,
            outputDirectory,
            overwrite: false,
            progress: null,
            token: CancellationToken.None));

        Assert.That(File.ReadAllText(Path.Combine(outputDirectory, "payload.bin")), Is.EqualTo("first"));
    }

    [Test]
    public async Task Extract_RejectsCorruptLaterDuplicateAndPreservesLastValidOutput() {
        var archiveFileName = Sandbox.PathFor("duplicate-corrupt.zip");
        var archiveBytes = CreateArchive(new[] {
            ("payload.bin", Encoding.UTF8.GetBytes("first"), CompressionLevel.NoCompression),
            ("payload.bin", Encoding.UTF8.GetBytes("last valid"), CompressionLevel.NoCompression),
            ("payload.bin", Encoding.UTF8.GetBytes("corrupt later duplicate"), CompressionLevel.NoCompression)
        });
        archiveBytes[LocalDataOffset(archiveBytes, entryIndex: 2)] ^= 1;
        File.WriteAllBytes(archiveFileName, archiveBytes);
        var outputDirectory = Sandbox.CreateDirectory("duplicate-corrupt-output");

        await Assert.ThrowsAsync<InvalidDataException>(() => ZipExtractor.ExtractAsync(
            archiveFileName,
            outputDirectory,
            overwrite: true,
            progress: null,
            token: CancellationToken.None));

        using (Assert.EnterMultipleScope()) {
            Assert.That(File.ReadAllText(Path.Combine(outputDirectory, "payload.bin")), Is.EqualTo("last valid"));
            Assert.That(Directory.EnumerateFiles(outputDirectory, ".brows-*.tmp"), Is.Empty);
        }
    }

    private sealed class CancelOnFirstBytesProgress : IOperationProgress {
        private readonly CancellationTokenSource Cancellation;

        private bool Cancelled;

        public long BytesCopied { get; private set; }

        public long BytesTarget { get; private set; }

        public CancelOnFirstBytesProgress(CancellationTokenSource cancellation) {
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
            var added = addProgress.GetValueOrDefault();
            BytesCopied += added;
            BytesTarget += addTarget.GetValueOrDefault();
            if (added > 0 && Cancelled is false) {
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
