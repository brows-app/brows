using Brows.Operations;
using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TarFsEntry = System.Formats.Tar.TarEntry;

namespace Brows.Commands;

[TestFixture]
internal sealed class TarTest {
    private TarTestSandbox Sandbox;

    [SetUp]
    public void SetUp() {
        Sandbox = new();
    }

    [TearDown]
    public void TearDown() {
        Sandbox.Dispose();
    }

    [TestCase(@"C:\panel", @"\\server\share\archive.tar", @"\\server\share\archive.tar")]
    [TestCase(@"C:\panel", @"D:\output\archive.tar", @"D:\output\archive.tar")]
    [TestCase(@"C:\panel", @"\archive.tar", @"C:\archive.tar")]
    [TestCase(@"C:\panel", @"archive.tar", @"C:\panel\archive.tar")]
    [TestCase(@"C:\panel", @"sub\archive", @"C:\panel\sub\archive.tar")]
    [TestCase(@"C:\panel", @"C:archive.tar", @"C:\panel\archive.tar")]
    [TestCase(@"D:\panel", @"C:archive.tar", @"C:\archive.tar")]
    public void ResolveOutputPath_PreservesThePathRootOrUsesThePanelDirectory(
        string directory,
        string output,
        string expected) {
        Assert.That(Tar.ResolveOutputPath(directory, output), Is.EqualTo(expected));
    }

    [Test]
    public async Task Create_PreservesSelectedTreeWhenExtracted() {
        var directory = Sandbox.CreateDirectory("selected");
        Sandbox.WriteFile("selected/child/deeper/file.txt", "nested contents");
        Sandbox.CreateDirectory("selected/child/deeper/empty");
        var input = new TarInput(true, new FileSystemInfo[] { new DirectoryInfo(directory) });
        var output = Sandbox.PathFor("result.tar");

        await TarArchiveFile.Create(
            output,
            input.Entries(null, CancellationToken.None).ToDictionary(
                entry => entry.EntryName,
                entry => entry.FilePath),
            null,
            CancellationToken.None);

        var extracted = Sandbox.PathFor("extracted");
        await TarArchiveFile.Extract(output, extracted, false, null, CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(File.ReadAllText(Path.Combine(extracted, "child", "deeper", "file.txt")),
                Is.EqualTo("nested contents"));
            Assert.That(Directory.Exists(Path.Combine(extracted, "child", "deeper", "empty")), Is.True);
        }
    }

    [Test]
    public async Task Create_CreatesAnExplicitEmptyDirectory() {
        var directory = Sandbox.CreateDirectory("empty");
        var timestamp = new DateTime(2020, 1, 2, 3, 4, 6, DateTimeKind.Local);
        Directory.SetLastWriteTime(directory, timestamp);
        var input = new TarInput(true, new FileSystemInfo[] {
            new DirectoryInfo(directory)
        });
        var output = Sandbox.PathFor("result.tar");

        await TarArchiveFile.Create(
            output,
            input.Entries(null, CancellationToken.None).ToDictionary(
                entry => entry.EntryName,
                entry => entry.FilePath),
            null,
            CancellationToken.None);

        using var stream = new FileStream(output, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new TarReader(stream);
        var entry = await reader.GetNextEntryAsync();
        using (Assert.EnterMultipleScope()) {
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.Name, Is.EqualTo("empty").Or.EqualTo("empty/"));
            Assert.That(entry.EntryType, Is.EqualTo(TarEntryType.Directory));
            Assert.That(entry.ModificationTime.UtcDateTime, Is.EqualTo(timestamp.ToUniversalTime()));
        }
    }

    [Test]
    public async Task Create_ReplacesExistingEntriesAndKeepsTheOthers() {
        var source = Sandbox.WriteFile("source.txt", "new contents");
        var output = Sandbox.PathFor("existing.tar");
        var original = Sandbox.WriteFile("replace.txt", "old contents");
        await TarArchiveFile.Create(output, new Dictionary<string, string> {
            { "replace.txt", original },
            { "keep.txt", Sandbox.WriteFile("keep.txt", "keep") }
        }, null, CancellationToken.None);

        await TarArchiveFile.Create(output, new Dictionary<string, string> {
            { "replace.txt", source }
        }, null, CancellationToken.None);

        var extracted = Sandbox.CreateDirectory("extracted");
        await TarArchiveFile.Extract(output, extracted, overwrite: true, null, CancellationToken.None);
        using (Assert.EnterMultipleScope()) {
            Assert.That(File.ReadAllText(Path.Combine(extracted, "replace.txt")), Is.EqualTo("new contents"));
            Assert.That(File.ReadAllText(Path.Combine(extracted, "keep.txt")), Is.EqualTo("keep"));
        }
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
}
