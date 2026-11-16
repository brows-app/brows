using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed partial class TarArchivePathTest {
    private static byte[] CreateArchiveBytes(string entryName, string contents) {
        using var stream = new MemoryStream();
        using (var writer = new TarWriter(stream, leaveOpen: true)) {
            var entry = new PaxTarEntry(TarEntryType.RegularFile, entryName);
            entry.DataStream = new MemoryStream(Encoding.UTF8.GetBytes(contents), writable: false);
            writer.WriteEntry(entry);
        }
        return stream.ToArray();
    }

    private static void CreateRawArchiveWithEntries(
        string archivePath,
        params (string Path, byte[] Contents, DateTimeOffset ModifiedTime)[] entries) {
        using (var stream = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
            using var writer = new TarWriter(stream);
            foreach (var item in entries) {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, item.Path) {
                    ModificationTime = item.ModifiedTime
                };
                using var content = new MemoryStream(item.Contents, writable: false);
                entry.DataStream = content;
                writer.WriteEntry(entry);
            }
        }
    }

    [Test]
    public async Task Update_DeleteFilePreservesSameNameDirectoryAndDescendants() {
        var archivePath = Sandbox.PathFor("delete-file.tar");
        CreateRawArchive(archivePath,
            ("a", TarEntryType.RegularFile, "file"),
            ("a/", TarEntryType.Directory, ""),
            ("a/keep.txt", TarEntryType.RegularFile, "child"));
        var path = Open(archivePath);
        try {
            var items = await path.EntryInfo(CancellationToken.None);
            var selectedFile = items.Items.Single(item =>
                item.Kind == TarEntryKind.File && item.Name.Normalized == "a");
            await path.Update(progress: null, token: CancellationToken.None, info: new TarArchiveUpdate {
                DeleteSelectedEntries = new[] { selectedFile }
            });

            var remaining = (await path.EntryInfo(CancellationToken.None)).Items;
            using (Assert.EnterMultipleScope()) {
                Assert.That(remaining, Has.Count.EqualTo(2));
                Assert.That(remaining.Any(item =>
                    item.Kind == TarEntryKind.Path && item.Name.Normalized == "a"), Is.True);
                Assert.That(remaining.Any(item =>
                    item.Kind == TarEntryKind.File && item.Name.Normalized == Path.Combine("a", "keep.txt")), Is.True);
                Assert.That(remaining.Any(item =>
                    item.Kind == TarEntryKind.File && item.Name.Normalized == "a"), Is.False);
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_DeleteDirectoryPreservesSameNameFile() {
        var archivePath = Sandbox.PathFor("delete-directory.tar");
        CreateRawArchive(archivePath,
            ("a", TarEntryType.RegularFile, "file"),
            ("a/", TarEntryType.Directory, ""),
            ("a/keep.txt", TarEntryType.RegularFile, "child"));
        var path = Open(archivePath);
        try {
            var items = await path.EntryInfo(CancellationToken.None);
            var selectedDirectory = items.Items.Single(item =>
                item.Kind == TarEntryKind.Path && item.Name.Normalized == "a");
            await path.Update(progress: null, token: CancellationToken.None, info: new TarArchiveUpdate {
                DeleteSelectedEntries = new[] { selectedDirectory }
            });

            var remaining = (await path.EntryInfo(CancellationToken.None)).Items;
            using (Assert.EnterMultipleScope()) {
                Assert.That(remaining, Has.Count.EqualTo(1));
                Assert.That(remaining.Single().Kind, Is.EqualTo(TarEntryKind.File));
                Assert.That(remaining.Single().Name.Normalized, Is.EqualTo("a"));
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Read_UsesLatestNormalizedDuplicateEntryAndMetadata() {
        var archivePath = Sandbox.PathFor("duplicates.tar");
        var oldTime = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var latestTime = new DateTimeOffset(2024, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var latestContents = "latest contents";
        CreateRawArchiveWithEntries(
            archivePath,
            ("folder\\value.txt", Encoding.UTF8.GetBytes("old"), oldTime),
            ("folder/value.txt", Encoding.UTF8.GetBytes(latestContents), latestTime));
        var path = Open(archivePath);
        try {
            var item = (await path.EntryInfo(CancellationToken.None)).Items
                .Single(entry => entry.Kind == TarEntryKind.File);
            var destination = Sandbox.PathFor("extracted/latest.txt");
            await path.Read(progress: null, token: CancellationToken.None, info: new TarArchiveRead {
                ExtractEntriesToFiles = new Dictionary<string, string> {
                    { item.Name.Original, destination }
                }
            });

            using (Assert.EnterMultipleScope()) {
                Assert.That(item.Name.Original, Is.EqualTo("folder/value.txt"));
                Assert.That(item.SizeOriginal, Is.EqualTo(latestContents.Length));
                Assert.That(item.LastWriteTime, Is.EqualTo(latestTime));
                Assert.That(File.ReadAllText(destination), Is.EqualTo(latestContents));
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Read_UsesLatestExactDuplicateEntry() {
        var archivePath = Sandbox.PathFor("exact-duplicates.tar");
        var oldTime = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var latestTime = new DateTimeOffset(2024, 6, 7, 8, 9, 10, TimeSpan.Zero);
        CreateRawArchiveWithEntries(
            archivePath,
            ("value.txt", Encoding.UTF8.GetBytes("old"), oldTime),
            ("value.txt", Encoding.UTF8.GetBytes("latest"), latestTime));
        var path = Open(archivePath);
        try {
            var item = (await path.EntryInfo(CancellationToken.None)).Items
                .Single(entry => entry.Kind == TarEntryKind.File);
            var destination = Sandbox.PathFor("extracted/exact.txt");
            await path.Read(progress: null, token: CancellationToken.None, info: new TarArchiveRead {
                ExtractEntriesToFiles = new Dictionary<string, string> {
                    { item.Name.Original, destination }
                }
            });

            using (Assert.EnterMultipleScope()) {
                Assert.That(item.SizeOriginal, Is.EqualTo("latest".Length));
                Assert.That(item.LastWriteTime, Is.EqualTo(latestTime));
                Assert.That(File.ReadAllText(destination), Is.EqualTo("latest"));
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Read_UsesLatestDuplicateWhenOpeningNestedArchive() {
        var archivePath = Sandbox.PathFor("nested-duplicates.tar");
        var oldTime = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var latestTime = new DateTimeOffset(2024, 6, 7, 8, 9, 10, TimeSpan.Zero);
        CreateRawArchiveWithEntries(
            archivePath,
            ("inner.tar", CreateArchiveBytes("value.txt", "old nested contents"), oldTime),
            ("inner.tar", CreateArchiveBytes("value.txt", "latest nested contents"), latestTime));
        var path = OpenNested(archivePath, "inner.tar");
        try {
            var item = (await path.EntryInfo(CancellationToken.None)).Items
                .Single(entry => entry.Kind == TarEntryKind.File);
            var destination = Sandbox.PathFor("extracted/nested.txt");
            await path.Read(progress: null, token: CancellationToken.None, info: new TarArchiveRead {
                ExtractEntriesToFiles = new Dictionary<string, string> {
                    { item.Name.Original, destination }
                }
            });

            Assert.That(File.ReadAllText(destination), Is.EqualTo("latest nested contents"));
        }
        finally {
            path.Release();
        }
    }
}
