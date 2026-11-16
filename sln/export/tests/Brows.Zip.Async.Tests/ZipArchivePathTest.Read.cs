using Brows.Entries;
using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
internal sealed partial class ZipArchivePathTest {
    private static byte[] CreateArchive(byte[] contents, CompressionLevel compressionLevel) {
        return CreateArchive("payload.bin", contents, compressionLevel);
    }

    private static byte[] CreateArchive(string entryName, byte[] contents, CompressionLevel compressionLevel) {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true)) {
            var entry = archive.CreateEntry(entryName, compressionLevel);
            using var entryStream = entry.Open();
            entryStream.Write(contents);
        }
        return stream.ToArray();
    }

    private static int LocalDataOffset(byte[] archive) {
        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(archive.AsSpan(26, 2));
        var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(archive.AsSpan(28, 2));
        return 30 + nameLength + extraLength;
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

    private static byte[] FindDeflateMutation(byte[] archive, byte[] expected, bool shouldThrow) {
        var dataOffset = LocalDataOffset(archive);
        var compressedLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(archive.AsSpan(18, 4)));
        for (var offset = dataOffset; offset < dataOffset + compressedLength; offset++) {
            for (var bit = 1; bit <= 0x80; bit <<= 1) {
                var candidate = (byte[])archive.Clone();
                candidate[offset] ^= (byte)bit;
                try {
                    var actual = ReadArchiveEntry(candidate);
                    var changed = actual.AsSpan().SequenceEqual(expected) == false;
                    if (shouldThrow == false && changed) {
                        return candidate;
                    }
                }
                catch (InvalidDataException) {
                    if (shouldThrow) {
                        return candidate;
                    }
                }
            }
        }
        throw new InvalidOperationException("No suitable deflate corruption could be produced.");
    }

    private static ZipArchivePath Open(string archiveFileName) {
        return ZipArchivePath.Get(new FileInfo(archiveFileName), new ZipArchiveNest(Array.Empty<string>()));
    }

    [Test]
    public async Task Read_RejectsStoredPayloadCorruptionAndPreservesTheDestination() {
        using var sandbox = new UpdateTestSandbox();
        var expected = Encoding.UTF8.GetBytes("UNIQUE ZIP CRC PAYLOAD");
        var archiveBytes = CreateArchive(expected, CompressionLevel.NoCompression);
        archiveBytes[LocalDataOffset(archiveBytes)] ^= 1;
        var archiveFileName = sandbox.PathFor("stored.zip");
        File.WriteAllBytes(archiveFileName, archiveBytes);
        var destination = sandbox.WriteFile("output/payload.bin", "existing destination");
        var path = Open(archiveFileName);
        try {
            var info = new ZipArchiveRead {
                ExtractOverwrites = true,
                ExtractEntriesToFiles = new() {
                    { "payload.bin", destination }
                }
            };
            await Assert.ThrowsAsync<InvalidDataException>(() => path.Read(info, null, CancellationToken.None));
            using (Assert.EnterMultipleScope()) {
                Assert.That(File.ReadAllText(destination), Is.EqualTo("existing destination"));
                Assert.That(Directory.EnumerateFiles(Path.GetDirectoryName(destination)!, ".brows-*.tmp"), Is.Empty);
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Read_RejectsCorruptOuterNestedEntryAndPreservesTheDestination() {
        using var sandbox = new UpdateTestSandbox();
        var innerContents = Encoding.UTF8.GetBytes("valid nested file content");
        var innerArchive = CreateArchive(innerContents, CompressionLevel.NoCompression);
        var outerArchive = CreateArchive("inner.zip", innerArchive, CompressionLevel.NoCompression);
        var expectedCrc = BinaryPrimitives.ReadUInt32LittleEndian(outerArchive.AsSpan(14, 4));
        var corruptCrc = expectedCrc ^ 1;
        BinaryPrimitives.WriteUInt32LittleEndian(outerArchive.AsSpan(14, 4), corruptCrc);
        var centralOffset = CentralHeaderOffset(outerArchive);
        BinaryPrimitives.WriteUInt32LittleEndian(outerArchive.AsSpan(centralOffset + 16, 4), corruptCrc);
        var archiveFileName = sandbox.PathFor("outer.zip");
        File.WriteAllBytes(archiveFileName, outerArchive);
        var destination = sandbox.WriteFile("output/payload.bin", "preserve nested output");
        var path = ZipArchivePath.Get(
            new FileInfo(archiveFileName),
            new ZipArchiveNest(new[] { "inner.zip" }));
        try {
            var info = new ZipArchiveRead {
                ExtractOverwrites = true,
                ExtractEntriesToFiles = new() {
                    { "payload.bin", destination }
                }
            };
            await Assert.ThrowsAsync<InvalidDataException>(() => path.Read(info, null, CancellationToken.None));
            Assert.That(File.ReadAllText(destination), Is.EqualTo("preserve nested output"));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task StreamSource_FailedNestedReadReleasesTheArchiveLock() {
        using var sandbox = new UpdateTestSandbox();
        var innerContents = Encoding.UTF8.GetBytes("valid nested file content");
        var innerArchive = CreateArchive(innerContents, CompressionLevel.NoCompression);
        var outerArchive = CreateArchive("inner.zip", innerArchive, CompressionLevel.NoCompression);
        var expectedCrc = BinaryPrimitives.ReadUInt32LittleEndian(outerArchive.AsSpan(14, 4));
        var corruptCrc = expectedCrc ^ 1;
        BinaryPrimitives.WriteUInt32LittleEndian(outerArchive.AsSpan(14, 4), corruptCrc);
        var centralOffset = CentralHeaderOffset(outerArchive);
        BinaryPrimitives.WriteUInt32LittleEndian(outerArchive.AsSpan(centralOffset + 16, 4), corruptCrc);
        var archiveFileName = sandbox.PathFor("outer-lock.zip");
        File.WriteAllBytes(archiveFileName, outerArchive);
        var path = ZipArchivePath.Get(
            new FileInfo(archiveFileName),
            new ZipArchiveNest(new[] { "inner.zip" }));
        try {
            using var innerStream = new MemoryStream(innerArchive, writable: false);
            using var innerZip = new ZipArchive(innerStream, ZipArchiveMode.Read);
            var innerEntry = innerZip.GetEntry("payload.bin");
            Assert.That(innerEntry, Is.Not.Null);
            var entryInfo = ZipEntryInfo.File(path, new ZipEntryName("payload.bin"), innerEntry!);
            var source = new TestArchiveStreamSource(entryInfo);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                ((IEntryStreamSource)source).StreamReady(CancellationToken.None));

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var destination = sandbox.WriteFile("output/payload.bin", "preserve nested output");
            var info = new ZipArchiveRead {
                ExtractOverwrites = true,
                ExtractEntriesToFiles = new() {
                    { "payload.bin", destination }
                }
            };
            await Assert.ThrowsAsync<InvalidDataException>(() => path.Read(info, null, timeout.Token));
            Assert.That(File.ReadAllText(destination), Is.EqualTo("preserve nested output"));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Read_RejectsDeflatePayloadCorruption() {
        using var sandbox = new UpdateTestSandbox();
        var expected = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("deflated payload data ", 64)));
        var archiveBytes = CreateArchive(expected, CompressionLevel.Optimal);
        var corrupted = FindDeflateMutation(archiveBytes, expected, shouldThrow: false);
        var archiveFileName = sandbox.PathFor("deflated.zip");
        File.WriteAllBytes(archiveFileName, corrupted);
        var destination = sandbox.PathFor("output/payload.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var path = Open(archiveFileName);
        try {
            var info = new ZipArchiveRead {
                ExtractEntriesToFiles = new() {
                    { "payload.bin", destination }
                }
            };
            await Assert.ThrowsAsync<InvalidDataException>(() => path.Read(info, null, CancellationToken.None));
            Assert.That(File.Exists(destination), Is.False);
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Read_RejectsAStoredEntryWhoseDeclaredLengthIsTooShort() {
        using var sandbox = new UpdateTestSandbox();
        var contents = Encoding.UTF8.GetBytes("declared length is too short");
        var archiveBytes = CreateArchive(contents, CompressionLevel.NoCompression);
        var centralOffset = CentralHeaderOffset(archiveBytes);
        BinaryPrimitives.WriteUInt32LittleEndian(
            archiveBytes.AsSpan(centralOffset + 24, 4),
            checked((uint)(contents.Length - 1)));
        var archiveFileName = sandbox.PathFor("short.zip");
        File.WriteAllBytes(archiveFileName, archiveBytes);
        var destination = sandbox.PathFor("output/payload.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var path = Open(archiveFileName);
        try {
            var info = new ZipArchiveRead {
                ExtractEntriesToFiles = new() {
                    { "payload.bin", destination }
                }
            };
            await Assert.ThrowsAsync<InvalidDataException>(() => path.Read(info, null, CancellationToken.None));
            Assert.That(File.Exists(destination), Is.False);
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Read_RejectsADeflatedEntryWhoseDeclaredLengthIsTooLong() {
        using var sandbox = new UpdateTestSandbox();
        var contents = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("declared deflate length ", 32)));
        var archiveBytes = CreateArchive(contents, CompressionLevel.Optimal);
        var centralOffset = CentralHeaderOffset(archiveBytes);
        BinaryPrimitives.WriteUInt32LittleEndian(
            archiveBytes.AsSpan(centralOffset + 24, 4),
            checked((uint)(contents.Length + 1)));
        var archiveFileName = sandbox.PathFor("long-deflate.zip");
        File.WriteAllBytes(archiveFileName, archiveBytes);
        var destination = sandbox.PathFor("output/payload.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var path = Open(archiveFileName);
        try {
            var info = new ZipArchiveRead {
                ExtractEntriesToFiles = new() {
                    { "payload.bin", destination }
                }
            };
            await Assert.ThrowsAsync<InvalidDataException>(() => path.Read(info, null, CancellationToken.None));
            Assert.That(File.Exists(destination), Is.False);
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Read_RejectsInvalidDeflateWithoutTruncatingTheDestination() {
        using var sandbox = new UpdateTestSandbox();
        var expected = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("deflated payload data ", 64)));
        var archiveBytes = CreateArchive(expected, CompressionLevel.Optimal);
        var corrupted = FindDeflateMutation(archiveBytes, expected, shouldThrow: true);
        var archiveFileName = sandbox.PathFor("invalid-deflate.zip");
        File.WriteAllBytes(archiveFileName, corrupted);
        var destination = sandbox.WriteFile("output/payload.bin", "preserve this sentinel");
        var path = Open(archiveFileName);
        try {
            var info = new ZipArchiveRead {
                ExtractOverwrites = true,
                ExtractEntriesToFiles = new() {
                    { "payload.bin", destination }
                }
            };
            await Assert.ThrowsAsync<InvalidDataException>(() => path.Read(info, null, CancellationToken.None));
            using (Assert.EnterMultipleScope()) {
                Assert.That(File.ReadAllText(destination), Is.EqualTo("preserve this sentinel"));
                Assert.That(Directory.EnumerateFiles(Path.GetDirectoryName(destination)!, ".brows-*.tmp"), Is.Empty);
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task StreamSource_RejectsCorruptEntryWhenCopiedCompletely() {
        using var sandbox = new UpdateTestSandbox();
        var expected = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("UNIQUE ZIP STREAM CRC PAYLOAD ", 32)));
        var archiveBytes = CreateArchive(expected, CompressionLevel.Optimal);
        var corrupted = FindDeflateMutation(archiveBytes, expected, shouldThrow: false);
        var archiveFileName = sandbox.PathFor("source.zip");
        File.WriteAllBytes(archiveFileName, corrupted);
        var path = Open(archiveFileName);
        try {
            var entry = (await path.EntryInfo(CancellationToken.None))
                .Items.Single(item => item.Kind == ZipEntryKind.File);
            var source = new TestArchiveStreamSource(entry);
            using var ready = await ((IEntryStreamSource)source).StreamReady(CancellationToken.None);
            await using var stream = ((IEntryStreamSource)source).Stream;
            await Assert.ThrowsAsync<InvalidDataException>(async () => {
                await stream.CopyToAsync(Stream.Null, CancellationToken.None);
            });
        }
        finally {
            path.Release();
        }
    }

    private sealed class TestArchiveStreamSource : ZipArchivePath.StreamSource<TestEntry> {
        public sealed override string RelativePath => EntryInfo.Name.Original;

        public TestArchiveStreamSource(ZipEntryInfo entryInfo) : base(new TestEntry(), entryInfo) {
        }
    }

    private sealed class TestEntry : IEntry {
        public event EntryDropEventHandler Drop {
            add { }
            remove { }
        }
        public event EntrySelectedEventHandler Selected {
            add { }
            remove { }
        }
        public event EntryRefreshedEventHandler Refreshed {
            add { }
            remove { }
        }

        public IEntryData this[string key] => null;
        public string ID => "test";
        public string Name => "payload.bin";
        public bool Select { get; set; }

        public Task Refresh(CancellationToken token) {
            return System.Threading.Tasks.Task.CompletedTask;
        }

        public Task<T> Task<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken token) {
            return factory(token);
        }
    }
}
