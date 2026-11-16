using Brows.SevenZip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

/// <summary>
/// Tests the operations of the plugin that work on the file of a 7-Zip
/// archive. The tests use real archives that are created and removed on the
/// real file system.
/// </summary>
[TestFixture]
internal sealed class SevenZipArchiveFileTest {
    private SevenZipTestSandbox Sandbox;

    private async Task<string> CreateArchiveWithOriginalContent() {
        var archivePath = Sandbox.PathFor("test.7z");
        await SevenZipArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "important.txt", Sandbox.WriteFile("original.txt", "original content") }
        }, null, null, CancellationToken.None);
        return archivePath;
    }

    private async Task AssertOriginalArchivePreserved(string archivePath, byte[] originalArchive) {
        Assert.That(File.ReadAllBytes(archivePath), Is.EqualTo(originalArchive));
        var directory = Sandbox.CreateDirectory("preserved");
        await SevenZipArchiveFile.Extract(archivePath, directory, false, null, CancellationToken.None);
        Assert.That(File.ReadAllText(Path.Combine(directory, "important.txt")), Is.EqualTo("original content"));
    }

    [SetUp]
    public void SetUp() {
        Sandbox = new();
    }

    [TearDown]
    public void TearDown() {
        Sandbox.Dispose();
    }

    [Test]
    public async Task Create_WritesAnArchiveThatHoldsTheFiles() {
        var archivePath = Sandbox.PathFor("test.7z");
        var files = new Dictionary<string, string> {
            { "hello.txt", Sandbox.WriteFile("hello.txt", "hello") }
        };
        await SevenZipArchiveFile.Create(archivePath, files, null, null, CancellationToken.None);
        Assert.That(File.Exists(archivePath), Is.True);
        Assert.That(new FileInfo(archivePath).Length, Is.GreaterThan(0));
    }

    [Test]
    public async Task Create_HoldsTheNameOfEveryFile() {
        var archivePath = Sandbox.PathFor("test.7z");
        var files = new Dictionary<string, string> {
            { "folder/hello.txt", Sandbox.WriteFile("hello.txt", "hello") }
        };
        await SevenZipArchiveFile.Create(archivePath, files, null, null, CancellationToken.None);
        var directory = Sandbox.CreateDirectory("extracted");
        await SevenZipArchiveFile.Extract(archivePath, directory, false, null, CancellationToken.None);
        Assert.That(File.ReadAllText(Path.Combine(directory, "folder", "hello.txt")), Is.EqualTo("hello"));
    }

    [Test]
    public async Task Create_AddsTheFilesToAnArchiveThatExists() {
        var archivePath = Sandbox.PathFor("test.7z");
        await SevenZipArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "first.txt", Sandbox.WriteFile("first.txt", "first") }
        }, null, null, CancellationToken.None);
        await SevenZipArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "second.txt", Sandbox.WriteFile("second.txt", "second") }
        }, null, null, CancellationToken.None);
        var directory = Sandbox.CreateDirectory("extracted");
        await SevenZipArchiveFile.Extract(archivePath, directory, false, null, CancellationToken.None);
        using (Assert.EnterMultipleScope()) {
            Assert.That(File.ReadAllText(Path.Combine(directory, "first.txt")), Is.EqualTo("first"));
            Assert.That(File.ReadAllText(Path.Combine(directory, "second.txt")), Is.EqualTo("second"));
        }
    }

    [Test]
    public async Task Create_UpdatesAnArchiveWhenThePathUsesDifferentCasing() {
        var archivePath = Sandbox.PathFor("test.7z");
        await SevenZipArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "first.txt", Sandbox.WriteFile("first.txt", "first") }
        }, null, null, CancellationToken.None);
        var caseVariant = Path.Combine(
            Path.GetDirectoryName(archivePath),
            Path.GetFileName(archivePath).ToUpperInvariant());
        if (File.Exists(caseVariant) == false) {
            Assert.Ignore("The test directory is case-sensitive.");
        }
        await SevenZipArchiveFile.Create(caseVariant, new Dictionary<string, string> {
            { "second.txt", Sandbox.WriteFile("second.txt", "second") }
        }, null, null, CancellationToken.None);
        var directory = Sandbox.CreateDirectory("case-variant-extracted");
        await SevenZipArchiveFile.Extract(archivePath, directory, false, null, CancellationToken.None);
        using (Assert.EnterMultipleScope()) {
            Assert.That(File.ReadAllText(Path.Combine(directory, "first.txt")), Is.EqualTo("first"));
            Assert.That(File.ReadAllText(Path.Combine(directory, "second.txt")), Is.EqualTo("second"));
        }
    }

    [Test]
    public async Task Create_ReplacesTheFileThatUsesTheSameName() {
        var archivePath = Sandbox.PathFor("test.7z");
        await SevenZipArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "first.txt", Sandbox.WriteFile("first.txt", "first") }
        }, null, null, CancellationToken.None);
        await SevenZipArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "first.txt", Sandbox.WriteFile("other.txt", "other") }
        }, null, null, CancellationToken.None);
        var directory = Sandbox.CreateDirectory("extracted");
        await SevenZipArchiveFile.Extract(archivePath, directory, false, null, CancellationToken.None);
        Assert.That(File.ReadAllText(Path.Combine(directory, "first.txt")), Is.EqualTo("other"));
    }

    [Test]
    public async Task Create_FailsWhenReplacementSourceIsMissingAndPreservesTheArchive() {
        var archivePath = await CreateArchiveWithOriginalContent();
        var originalArchive = File.ReadAllBytes(archivePath);
        await Assert.CatchAsync<Exception>(() => SevenZipArchiveFile.Create(
            archivePath,
            new Dictionary<string, string> {
                { "important.txt", Sandbox.PathFor("missing.txt") }
            },
            null,
            null,
            CancellationToken.None));
        await AssertOriginalArchivePreserved(archivePath, originalArchive);
    }

    [Test]
    public async Task Create_FailsWhenReplacementSourceWasDeletedAndPreservesTheArchive() {
        var archivePath = await CreateArchiveWithOriginalContent();
        var originalArchive = File.ReadAllBytes(archivePath);
        var sourcePath = Sandbox.WriteFile("replacement.txt", "replacement content");
        File.Delete(sourcePath);
        await Assert.CatchAsync<Exception>(() => SevenZipArchiveFile.Create(
            archivePath,
            new Dictionary<string, string> {
                { "important.txt", sourcePath }
            },
            null,
            null,
            CancellationToken.None));
        await AssertOriginalArchivePreserved(archivePath, originalArchive);
    }

    [Test]
    public async Task Create_FailsWhenReplacementSourceIsLockedAndPreservesTheArchive() {
        var archivePath = await CreateArchiveWithOriginalContent();
        var originalArchive = File.ReadAllBytes(archivePath);
        var sourcePath = Sandbox.WriteFile("replacement.txt", "replacement content");
        using (var locked = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.None)) {
            await Assert.CatchAsync<Exception>(() => SevenZipArchiveFile.Create(
                archivePath,
                new Dictionary<string, string> {
                    { "important.txt", sourcePath }
                },
                null,
                null,
                CancellationToken.None));
        }
        await AssertOriginalArchivePreserved(archivePath, originalArchive);
    }

    [Test]
    public async Task Create_FailsWhenEmptyReplacementSourceIsLockedAndPreservesTheArchive() {
        var archivePath = await CreateArchiveWithOriginalContent();
        var originalArchive = File.ReadAllBytes(archivePath);
        var sourcePath = Sandbox.WriteFile("replacement.txt", string.Empty);
        using (var locked = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.None)) {
            await Assert.CatchAsync<Exception>(() => SevenZipArchiveFile.Create(
                archivePath,
                new Dictionary<string, string> {
                    { "important.txt", sourcePath }
                },
                null,
                null,
                CancellationToken.None));
        }
        await AssertOriginalArchivePreserved(archivePath, originalArchive);
    }

    [Test]
    public async Task Create_FailsWhenSourceIsLockedAndPreservesAnExistingValidEmptyArchive() {
        var archivePath = Sandbox.PathFor("empty.7z");
        SevenZipArchive.Create(archivePath, Array.Empty<SevenZipUpdateItem>(), null);
        var originalArchive = File.ReadAllBytes(archivePath);
        var sourcePath = Sandbox.WriteFile("replacement.txt", "replacement content");
        using (var locked = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.None)) {
            await Assert.CatchAsync<Exception>(() => SevenZipArchiveFile.Create(
                archivePath,
                new Dictionary<string, string> {
                    { "replacement.txt", sourcePath }
                },
                null,
                null,
                CancellationToken.None));
        }
        Assert.That(File.ReadAllBytes(archivePath), Is.EqualTo(originalArchive));
        using (var archive = SevenZipArchive.Open(archivePath)) {
            Assert.That(archive.Entries, Is.Empty);
        }
    }

    [Test]
    public async Task Create_PreservesPreexistingTemporarySiblingWhenUpdateSucceeds() {
        var archivePath = await CreateArchiveWithOriginalContent();
        var temporaryPath = archivePath + ".tmp";
        var sentinel = new byte[] { 0x42, 0x72, 0x6f, 0x77, 0x73 };
        File.WriteAllBytes(temporaryPath, sentinel);
        await SevenZipArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "second.txt", Sandbox.WriteFile("second.txt", "second content") }
        }, null, null, CancellationToken.None);
        Assert.That(File.Exists(temporaryPath), Is.True);
        Assert.That(File.ReadAllBytes(temporaryPath), Is.EqualTo(sentinel));
        var directory = Sandbox.CreateDirectory("updated");
        await SevenZipArchiveFile.Extract(archivePath, directory, false, null, CancellationToken.None);
        using (Assert.EnterMultipleScope()) {
            Assert.That(File.ReadAllText(Path.Combine(directory, "important.txt")), Is.EqualTo("original content"));
            Assert.That(File.ReadAllText(Path.Combine(directory, "second.txt")), Is.EqualTo("second content"));
        }
    }

    [Test]
    public async Task Create_UpdatesAnArchiveWithALongValidFileName() {
        var archiveName = new string('a', 240) + ".7z";
        var archivePath = @"\\?\" + Sandbox.PathFor(archiveName);
        var probePath = @"\\?\" + Sandbox.PathFor(new string('p', 240) + ".probe");
        try {
            File.WriteAllBytes(probePath, Array.Empty<byte>());
        }
        catch (PathTooLongException) {
            Assert.Ignore("The test environment does not support this long file name.");
            return;
        }
        catch (IOException exception) when (exception.HResult == unchecked((int)0x800700CE)) {
            Assert.Ignore("The test environment does not support this long file name.");
            return;
        }
        finally {
            if (File.Exists(probePath)) {
                File.Delete(probePath);
            }
        }
        var initialArchivePath = Sandbox.PathFor("initial.7z");
        var originalSourcePath = Sandbox.WriteFile("original.txt", "original content");
        var originalInfo = new FileInfo(originalSourcePath);
        SevenZipArchive.Create(initialArchivePath, new[] {
            new SevenZipUpdateItem("original.txt", originalSourcePath, false, originalInfo.Length, null, 0)
        }, null);
        File.Move(initialArchivePath, archivePath);
        await SevenZipArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "second.txt", Sandbox.WriteFile("second.txt", "second content") }
        }, null, null, CancellationToken.None);
        using (var archive = SevenZipArchive.Open(archivePath)) {
            Assert.That(archive.Entries, Has.Count.EqualTo(2));
            Assert.That(archive.IndexOf("original.txt"), Is.GreaterThanOrEqualTo(0));
            Assert.That(archive.IndexOf("second.txt"), Is.GreaterThanOrEqualTo(0));
        }
    }

    [Test]
    public async Task Create_PreservesPreexistingTemporarySiblingWhenUpdateFails() {
        var archivePath = await CreateArchiveWithOriginalContent();
        var originalArchive = File.ReadAllBytes(archivePath);
        var temporaryPath = archivePath + ".tmp";
        var sentinel = new byte[] { 0x42, 0x72, 0x6f, 0x77, 0x73 };
        File.WriteAllBytes(temporaryPath, sentinel);
        var sourcePath = Sandbox.WriteFile("replacement.txt", "replacement content");
        using (var locked = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.None)) {
            await Assert.CatchAsync<Exception>(() => SevenZipArchiveFile.Create(
                archivePath,
                new Dictionary<string, string> {
                    { "important.txt", sourcePath }
                },
                null,
                null,
                CancellationToken.None));
        }
        Assert.That(File.Exists(temporaryPath), Is.True);
        Assert.That(File.ReadAllBytes(temporaryPath), Is.EqualTo(sentinel));
        await AssertOriginalArchivePreserved(archivePath, originalArchive);
    }

    [Test]
    public async Task Extract_WritesEveryItemOfTheArchiveToTheFolder() {
        var archivePath = Sandbox.PathFor("test.7z");
        await SevenZipArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "first.txt", Sandbox.WriteFile("first.txt", "first") },
            { "folder/second.txt", Sandbox.WriteFile("second.txt", "second") }
        }, null, null, CancellationToken.None);
        var directory = Sandbox.CreateDirectory("extracted");
        await SevenZipArchiveFile.Extract(archivePath, directory, false, null, CancellationToken.None);
        using (Assert.EnterMultipleScope()) {
            Assert.That(File.ReadAllText(Path.Combine(directory, "first.txt")), Is.EqualTo("first"));
            Assert.That(File.ReadAllText(Path.Combine(directory, "folder", "second.txt")), Is.EqualTo("second"));
        }
    }

    [Test]
    public async Task Extract_CreatesTheFolderThatIsNotThereYet() {
        var archivePath = Sandbox.PathFor("test.7z");
        await SevenZipArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "first.txt", Sandbox.WriteFile("first.txt", "first") }
        }, null, null, CancellationToken.None);
        var directory = Sandbox.PathFor("extracted");
        await SevenZipArchiveFile.Extract(archivePath, directory, false, null, CancellationToken.None);
        Assert.That(File.ReadAllText(Path.Combine(directory, "first.txt")), Is.EqualTo("first"));
    }
}
