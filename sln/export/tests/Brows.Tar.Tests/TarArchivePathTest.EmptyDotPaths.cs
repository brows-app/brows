using Brows.Entries;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed partial class TarArchivePathTest {
    [Test]
    public async Task Read_EmptyRegularFileCreatesAnEmptyFile() {
        var archivePath = await CreateArchive("empty-read.tar", ("empty.txt", ""));
        var path = Open(archivePath);
        try {
            var destination = Sandbox.PathFor("read-empty.txt");
            await path.Read(progress: null, token: CancellationToken.None, info: new TarArchiveRead {
                ExtractEntriesToFiles = new Dictionary<string, string> {
                    { "empty.txt", destination }
                }
            });
            using (Assert.EnterMultipleScope()) {
                Assert.That(File.Exists(destination), Is.True);
                Assert.That(new FileInfo(destination).Length, Is.Zero);
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task ExtractAll_EmptyRegularFileCreatesAnEmptyFile() {
        var archivePath = await CreateArchive("empty-all.tar", ("empty.txt", ""));
        var path = Open(archivePath);
        try {
            var destinationDirectory = Sandbox.CreateDirectory("empty-all");
            await path.ExtractAll(destinationDirectory, overwrite: false, progress: null, CancellationToken.None);
            var destination = Path.Combine(destinationDirectory, "empty.txt");
            using (Assert.EnterMultipleScope()) {
                Assert.That(File.Exists(destination), Is.True);
                Assert.That(new FileInfo(destination).Length, Is.Zero);
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task StreamReady_EmptyRegularFileExposesAnEmptyStream() {
        var archivePath = await CreateArchive("empty-stream.tar", ("empty.txt", ""));
        var (archive, source) = await CreateArchiveStreamSource(archivePath, "empty.txt");
        try {
            using var ready = await source.StreamReady(CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(source.Stream, Is.Not.Null);
                Assert.That(source.StreamLength, Is.Zero);
                Assert.That(source.Stream.ReadByte(), Is.EqualTo(-1));
            }
        }
        finally {
            archive.Release();
        }
    }

    [Test]
    public async Task Update_CopyStreamCopiesAnEmptyRegularFile() {
        var archivePath = await CreateArchive("empty-copy-source.tar", ("empty.txt", ""));
        var (archive, source) = await CreateArchiveStreamSource(archivePath, "empty.txt");
        var target = Open(Sandbox.PathFor("empty-copy-target.tar"));
        try {
            await target.Update(progress: null, token: CancellationToken.None, info: new TarArchiveUpdate {
                CopyStreamSets = new[] { new TarStreamSet(new[] { source }) }
            });
            var copied = (await target.EntryInfo(CancellationToken.None)).Items
                .Single(item => item.Kind == TarEntryKind.File && item.Name.Normalized == "empty.txt");
            var destination = Sandbox.PathFor("copied-empty.txt");
            await copied.Extract(destination, overwrite: false, CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(File.Exists(destination), Is.True);
                Assert.That(new FileInfo(destination).Length, Is.Zero);
            }
        }
        finally {
            target.Release();
            archive.Release();
        }
    }

    [Test]
    public async Task EntryInfo_DotRootMarkerDoesNotBecomeARecursiveDirectory() {
        var archivePath = Sandbox.PathFor("dot-root-list.tar");
        CreateRawArchive(archivePath,
            ("./", TarEntryType.Directory, ""),
            ("./folder/", TarEntryType.Directory, ""),
            ("./folder/file.txt", TarEntryType.RegularFile, "contents"));
        var path = Open(archivePath);
        try {
            var items = (await path.EntryInfo(CancellationToken.None)).Items;
            var names = items.Select(item => item.Name.Normalized).ToArray();
            using (Assert.EnterMultipleScope()) {
                Assert.That(names, Is.EquivalentTo(new[] {
                    "folder",
                    Path.Combine("folder", "file.txt")
                }));
                Assert.That(names, Does.Not.Contain(string.Empty));
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task ExtractAll_DotRootMarkerAndPrefixedFilesUseTheArchiveRoot() {
        var archivePath = Sandbox.PathFor("dot-root-extract.tar");
        CreateRawArchive(archivePath,
            ("./", TarEntryType.Directory, ""),
            ("./folder/", TarEntryType.Directory, ""),
            ("./folder/file.txt", TarEntryType.RegularFile, "contents"));
        var path = Open(archivePath);
        try {
            var destinationDirectory = Sandbox.CreateDirectory("dot-root-extract");
            await path.ExtractAll(destinationDirectory, overwrite: false, progress: null, CancellationToken.None);
            Assert.That(
                File.ReadAllText(Path.Combine(destinationDirectory, "folder", "file.txt")),
                Is.EqualTo("contents"));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Read_MatchesDotPrefixedEntryWithoutARootMarker() {
        var archivePath = Sandbox.PathFor("dot-file-without-root.tar");
        CreateRawArchive(archivePath, ("./file.txt", TarEntryType.RegularFile, "contents"));
        var path = Open(archivePath);
        try {
            var destination = Sandbox.PathFor("dot-file-read.txt");
            await path.Read(progress: null, token: CancellationToken.None, info: new TarArchiveRead {
                ExtractEntriesToFiles = new Dictionary<string, string> {
                    { "file.txt", destination }
                }
            });
            Assert.That(File.ReadAllText(destination), Is.EqualTo("contents"));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task ExtractAll_RejectsRootedEntryNames() {
        var archivePath = Sandbox.PathFor("rooted.tar");
        CreateRawArchive(archivePath, ("/rooted.txt", TarEntryType.RegularFile, "contents"));
        var path = Open(archivePath);
        try {
            var destinationDirectory = Sandbox.CreateDirectory("rooted-extract");
            await Assert.ThrowsAsync<InvalidDataException>(() => path.ExtractAll(
                destinationDirectory,
                overwrite: false,
                progress: null,
                token: CancellationToken.None));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_ReplacesDotPrefixedEntryByItsCanonicalName() {
        var archivePath = Sandbox.PathFor("dot-replace.tar");
        CreateRawArchive(archivePath, ("./file.txt", TarEntryType.RegularFile, "old"));
        var path = Open(archivePath);
        try {
            await path.Update(progress: null, token: CancellationToken.None, info: new TarArchiveUpdate {
                CreateEntriesFromFiles = new Dictionary<string, string> {
                    { "./file.txt", Sandbox.WriteFile("replacement.txt", "new") }
                }
            });
            var item = (await path.EntryInfo(CancellationToken.None)).Items
                .Single(entry => entry.Kind == TarEntryKind.File);
            var destination = Sandbox.PathFor("dot-replacement-output.txt");
            await item.Extract(destination, overwrite: false, CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(item.Name.Normalized, Is.EqualTo("file.txt"));
                Assert.That(File.ReadAllText(destination), Is.EqualTo("new"));
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_DeletesDotPrefixedEntryByItsCanonicalName() {
        var archivePath = Sandbox.PathFor("dot-delete.tar");
        CreateRawArchive(archivePath, ("./file.txt", TarEntryType.RegularFile, "contents"));
        var path = Open(archivePath);
        try {
            await path.Update(progress: null, token: CancellationToken.None, info: new TarArchiveUpdate {
                DeleteEntries = new[] { "file.txt" }
            });
            Assert.That((await path.EntryInfo(CancellationToken.None)).Items, Is.Empty);
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_RejectsWhitespaceOnlyEntryNames() {
        var archivePath = await CreateArchive("whitespace-path.tar", ("existing.txt", "contents"));
        await AssertUpdateRejected(archivePath, new TarArchiveUpdate {
            CreateEntriesFromFiles = new Dictionary<string, string> {
                { "   ", Sandbox.WriteFile("whitespace-source.txt", "new") }
            }
        });
    }
}
