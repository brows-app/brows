using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TarFsEntry = System.Formats.Tar.TarEntry;

namespace Brows;

[TestFixture]
internal sealed class TarArchiveFileTest {
    private TarTestSandbox Sandbox;

    [SetUp]
    public void SetUp() {
        Sandbox = new();
    }

    [TearDown]
    public void TearDown() {
        Sandbox.Dispose();
    }

    private static IReadOnlyList<(string Name, TarEntryType Kind)> ListEntries(string archivePath) {
        using var stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new TarReader(stream);
        var entries = new List<(string Name, TarEntryType Kind)>();
        TarFsEntry entry;
        while ((entry = reader.GetNextEntry()) is not null) {
            if (entry.EntryType is TarEntryType.GlobalExtendedAttributes) {
                continue;
            }
            entries.Add((entry.Name, entry.EntryType));
        }
        return entries;
    }

    [Test]
    public async Task Create_WritesTheFilesOfTheArchive() {
        var archivePath = Sandbox.PathFor("result.tar");
        await TarArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "first.txt", Sandbox.WriteFile("first.txt", "first") },
            { "folder/second.txt", Sandbox.WriteFile("folder/second.txt", "second") }
        }, null, CancellationToken.None);

        Assert.That(ListEntries(archivePath).Select(entry => entry.Name),
            Is.EquivalentTo(new[] { "first.txt", "folder/second.txt" }));
    }

    [Test]
    public async Task Create_RejectsEntryNamesThatAreNotRelative() {
        var archivePath = Sandbox.PathFor("result.tar");
        await Assert.ThrowsAsync<ArgumentException>(() => TarArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "../outside.txt", Sandbox.WriteFile("outside.txt", "contents") }
        }, null, CancellationToken.None));
        Assert.That(File.Exists(archivePath), Is.False);
    }

    [Test]
    public async Task Create_ThrowsWhenASourceFileIsMissing() {
        var archivePath = Sandbox.PathFor("result.tar");
        await Assert.ThrowsAsync<FileNotFoundException>(() => TarArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "first.txt", Sandbox.PathFor("missing.txt") }
        }, null, CancellationToken.None));
    }

    [Test]
    public async Task Create_ReplacesExistingFilesAndKeepsTheOthers() {
        var archivePath = await CreateArchive("result.tar", ("keep.txt", "keep"), ("replace.txt", "old"));
        await TarArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "replace.txt", Sandbox.WriteFile("replace.txt", "new") }
        }, null, CancellationToken.None);

        var archive = TarArchivePath.Get(TarArchiveIdentity.CreateFallback(new FileInfo(archivePath)));
        try {
            var items = await archive.EntryInfo(CancellationToken.None);
            Assert.That(items.Items.Select(item => item.Name.Original),
                Is.EquivalentTo(new[] { "keep.txt", "replace.txt" }));
            var destination = Sandbox.PathFor("replace-extracted.txt");
            await items.Items.Single(item => item.Name.Original == "replace.txt")
                .Extract(destination, overwrite: false, CancellationToken.None);
            Assert.That(File.ReadAllText(destination), Is.EqualTo("new"));
        }
        finally {
            archive.Release();
        }
    }

    [Test]
    public async Task Extract_WritesEveryItemOfTheArchive() {
        var archivePath = await CreateArchive("result.tar",
            ("first.txt", "first"),
            ("folder/second.txt", "second"));
        var directory = Sandbox.CreateDirectory("extracted");

        await TarArchiveFile.Extract(archivePath, directory, overwrite: false, null, CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(File.ReadAllText(Path.Combine(directory, "first.txt")), Is.EqualTo("first"));
            Assert.That(File.ReadAllText(Path.Combine(directory, "folder", "second.txt")), Is.EqualTo("second"));
        }
    }

    [Test]
    public async Task Extract_ReplacesExistingFilesWhenItOverwrites() {
        var archivePath = await CreateArchive("result.tar", ("first.txt", "first"));
        var directory = Sandbox.CreateDirectory("extracted");
        File.WriteAllText(Path.Combine(directory, "first.txt"), "previous");

        await TarArchiveFile.Extract(archivePath, directory, overwrite: true, null, CancellationToken.None);

        Assert.That(File.ReadAllText(Path.Combine(directory, "first.txt")), Is.EqualTo("first"));
    }

    private async Task<string> CreateArchive(string name, params (string Path, string Contents)[] files) {
        var archivePath = Sandbox.PathFor(name);
        await TarArchiveFile.Create(archivePath, files.ToDictionary(
            file => file.Path,
            file => Sandbox.WriteFile(file.Path, file.Contents)), null, CancellationToken.None);
        return archivePath;
    }
}
