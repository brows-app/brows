using Brows.Entries;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brows;

internal sealed partial class ZipArchivePathTest {
    private static void CreateMetadataArchive(string path, params (string Name, string Contents)[] members) {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        for (var index = 0; index < members.Length; index++) {
            var member = members[index];
            var entry = archive.CreateEntry(member.Name, CompressionLevel.NoCompression);
            entry.LastWriteTime = new DateTimeOffset(2020 + index, 1, 1, 0, 0, 0, TimeSpan.Zero);
            using var content = entry.Open();
            content.Write(Encoding.UTF8.GetBytes(member.Contents));
        }
    }

    [Test]
    public async Task EntryInfo_IncludesAncestorsOfExplicitDirectoriesWithoutFiles() {
        using var sandbox = new UpdateTestSandbox();
        var archiveFileName = sandbox.PathFor("directory-only.zip");
        CreateMetadataArchive(archiveFileName, ("parent/child/grandchild/", ""));
        var path = Open(archiveFileName);
        try {
            var items = (await path.EntryInfo(CancellationToken.None)).Items.ToArray();
            using (Assert.EnterMultipleScope()) {
                Assert.That(items.Select(item => item.Name.Normalized), Is.EquivalentTo(new[] {
                    "parent",
                    Path.Combine("parent", "child"),
                    Path.Combine("parent", "child", "grandchild")
                }));
                Assert.That(items.All(item => item.Kind == ZipEntryKind.Path), Is.True);
                Assert.That(items.Where(item => item.Name.Parent == "").Select(item => item.Name.Top),
                    Is.EqualTo(new[] { "parent" }));
            }
        }
        finally {
            path.Release();
        }
    }

    [TestCase("same.txt", "same.txt")]
    [TestCase("folder\\same.txt", "folder/same.txt")]
    [TestCase("folder/same.txt", "folder\\same.txt")]
    public async Task EntryInfo_CollidingNamesUseTheSameMemberAsExtraction(string firstName, string laterName) {
        using var sandbox = new UpdateTestSandbox();
        var archiveFileName = sandbox.PathFor("duplicates.zip");
        const string firstContents = "FIRST";
        CreateMetadataArchive(archiveFileName, (firstName, firstContents), (laterName, "SECOND-LONGER"));
        uint expectedCrc;
        using (var zip = ZipFile.OpenRead(archiveFileName)) {
            expectedCrc = zip.Entries[0].Crc32;
        }
        var path = Open(archiveFileName);
        try {
            var entry = (await path.EntryInfo(CancellationToken.None)).Items.Single(item =>
                item.Kind == ZipEntryKind.File);
            var destination = sandbox.PathFor("extracted.txt");
            await entry.Extract(destination, overwrite: false, CancellationToken.None);
            var source = new TestArchiveStreamSource(entry);
            using var ready = await ((IEntryStreamSource)source).StreamReady(CancellationToken.None);
            await using var stream = ((IEntryStreamSource)source).Stream;
            using var reader = new StreamReader(stream);
            var streamed = await reader.ReadToEndAsync(CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(entry.Name.Original, Is.EqualTo(firstName));
                Assert.That(entry.SizeOriginal, Is.EqualTo(firstContents.Length));
                Assert.That(entry.SizeCompressed, Is.EqualTo(firstContents.Length));
                Assert.That(entry.CRC32, Is.EqualTo((long)expectedCrc));
                Assert.That(entry.LastWriteTime!.Value.Year, Is.EqualTo(2020));
                Assert.That(File.ReadAllText(destination), Is.EqualTo(firstContents));
                Assert.That(streamed, Is.EqualTo(firstContents));
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_DeletingDuplicateNamesPreservesDifferentlyCasedMembers() {
        using var sandbox = new UpdateTestSandbox();
        var archiveFileName = sandbox.PathFor("delete-duplicates.zip");
        CreateMetadataArchive(archiveFileName,
            ("same.txt", "FIRST"), ("same.txt", "SECOND"), ("SAME.txt", "KEEP"));
        var path = Open(archiveFileName);
        try {
            await path.Update(new ZipArchiveUpdate { DeleteEntries = new() { "same.txt" } },
                null, CancellationToken.None);
            using var archive = ZipFile.OpenRead(archiveFileName);
            Assert.That(archive.Entries.Select(entry => entry.FullName), Is.EqualTo(new[] { "SAME.txt" }));
            using var reader = new StreamReader(archive.Entries.Single().Open());
            Assert.That(await reader.ReadToEndAsync(), Is.EqualTo("KEEP"));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_ReplacementRemovesAllOldDuplicatePayloads() {
        using var sandbox = new UpdateTestSandbox();
        var archiveFileName = sandbox.PathFor("replace-duplicates.zip");
        CreateMetadataArchive(archiveFileName, ("same.txt", "FIRST"), ("same.txt", "SECOND"));
        var replacement = sandbox.WriteFile("replacement.txt", "REPLACEMENT");
        var path = Open(archiveFileName);
        try {
            await path.Update(new ZipArchiveUpdate {
                CreateEntriesFromFiles = new() { ["same.txt"] = replacement }
            }, null, CancellationToken.None);
            using var archive = ZipFile.OpenRead(archiveFileName);
            Assert.That(archive.Entries.Select(entry => entry.FullName), Is.EqualTo(new[] { "same.txt" }));
            using var reader = new StreamReader(archive.Entries.Single().Open());
            Assert.That(await reader.ReadToEndAsync(), Is.EqualTo("REPLACEMENT"));
        }
        finally {
            path.Release();
        }
    }
}
