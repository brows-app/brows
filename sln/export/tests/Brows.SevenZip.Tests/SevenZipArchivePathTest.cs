using Brows.Entries;
using Brows.Operations;
using Brows.SevenZip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

/// <summary>
/// Tests the archive of the plugin. The tests use real archives that are
/// created and removed on the real file system.
/// </summary>
[TestFixture]
internal sealed class SevenZipArchivePathTest {
    private SevenZipTestSandbox Sandbox;

    [SetUp]
    public void SetUp() {
        Sandbox = new();
    }

    [TearDown]
    public void TearDown() {
        Sandbox.Dispose();
    }

    /// <summary>
    /// Creates an archive that holds the given files.
    /// </summary>
    private async Task<string> CreateArchive(string name, params (string Path, string Contents)[] files) {
        var archivePath = Sandbox.PathFor(name);
        await SevenZipArchiveFile.Create(archivePath, files.ToDictionary(
            file => file.Path,
            file => Sandbox.WriteFile(file.Path, file.Contents)), null, null, CancellationToken.None);
        return archivePath;
    }

    [Test]
    public async Task Get_UsesStructuredNestingForCacheIdentityAndRelease() {
        var archivePath = await CreateArchive("cache-identity.7z", ("keep.txt", "contents"));
        var identity = SevenZipArchiveIdentity.CreateFallback(new FileInfo(archivePath));
        var first = SevenZipArchivePath.Get(identity, new SevenZipArchiveNest(new[] { "a>b", "c" }));
        var second = SevenZipArchivePath.Get(identity, new SevenZipArchiveNest(new[] { "a", "b>c" }));
        var repeated = SevenZipArchivePath.Get(identity, new SevenZipArchiveNest(new[] { "a", "b>c" }));
        SevenZipArchivePath afterRelease = null;
        var firstReleased = false;

        try {
            using (Assert.EnterMultipleScope()) {
                Assert.That(first, Is.Not.SameAs(second));
                Assert.That(second, Is.SameAs(repeated));
                Assert.That(second.FullName, Is.EqualTo($"7z:1|f={identity.FullName}|n=a|n=b%3Ec|d="));
            }

            first.Release();
            firstReleased = true;
            afterRelease = SevenZipArchivePath.Get(
                identity,
                new SevenZipArchiveNest(new[] { "a", "b>c" }));

            Assert.That(afterRelease, Is.SameAs(second));
        }
        finally {
            if (firstReleased is false) {
                first.Release();
            }
            afterRelease?.Release();
            repeated.Release();
            second.Release();
        }
    }

    private string CreateArchiveWithDirectory(string name, string entryName) {
        var archivePath = Sandbox.PathFor(name);
        SevenZipArchive.Create(archivePath, new[] {
            new SevenZipUpdateItem(entryName, null, true, 0, null, 0)
        }, null);
        return archivePath;
    }

    private async Task<(SevenZipID Archive, IEntryStreamSource Source)> CreateArchiveStreamSource(
        string archivePath,
        string entryName) {
        var factory = new SevenZipProviderFactory();
        var archive = await SevenZipID.Parse(archivePath, factory, CancellationToken.None)
            ?? throw new InvalidOperationException("The source archive could not be opened.");
        try {
            var provider = new SevenZipProvider(archive, factory);
            var info = (await archive.EntryInfo(CancellationToken.None))
                .Single(item => item.Name.Original == entryName);
            var source = new SevenZipStreamSource(new SevenZipEntry(provider, info));
            return (archive, source);
        }
        catch {
            archive.Release();
            throw;
        }
    }

    private static IReadOnlySet<string> StagingDirectories() {
        var directory = Path.Combine(Path.GetTempPath(), "Brows", "SevenZip");
        return Directory.Exists(directory)
            ? Directory.GetDirectories(directory).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private static void RemoveNewStagingDirectories(IReadOnlySet<string> existing) {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Brows", "SevenZip"));
        foreach (var directory in StagingDirectories().Except(existing, StringComparer.OrdinalIgnoreCase)) {
            var fullDirectory = Path.GetFullPath(directory);
            var relative = Path.GetRelativePath(root, fullDirectory);
            var parentPrefix = ".." + Path.DirectorySeparatorChar;
            var outside = relative == "." ||
                          relative == ".." ||
                          relative.StartsWith(parentPrefix, StringComparison.Ordinal) ||
                          Path.IsPathRooted(relative);
            if (outside) {
                throw new InvalidOperationException(
                    "Refusing to remove a directory outside the SevenZip staging folder.");
            }
            foreach (var file in Directory.GetFiles(fullDirectory)) {
                File.Delete(file);
            }
            Directory.Delete(fullDirectory);
        }
    }

    private string CreateArchiveWithEntryName(string name) {
        var source = Sandbox.WriteFile("unsafe-source.txt", "content");
        var info = new FileInfo(source);
        var archivePath = Sandbox.PathFor("source.7z");
        SevenZipArchive.Create(archivePath, new[] {
            new SevenZipUpdateItem(name, source, false, info.Length, null, 0)
        }, null);
        return archivePath;
    }

    /// <summary>
    /// Opens the archive of the given location.
    /// </summary>
    private static SevenZipArchivePath Open(string archivePath) {
        return SevenZipArchivePath.GetForTest(new FileInfo(archivePath));
    }

    /// <summary>
    /// Opens the nested archive that the nesting names.
    /// </summary>
    private static SevenZipArchivePath OpenNested(string archivePath, params string[] nest) {
        var identity = SevenZipArchiveIdentity.CreateFallback(new FileInfo(archivePath));
        return SevenZipArchivePath.Get(identity, new SevenZipArchiveNest(nest));
    }

    private async Task AssertUpdateRejected(string archivePath, SevenZipArchiveUpdate info) {
        var originalArchive = File.ReadAllBytes(archivePath);
        var path = Open(archivePath);
        try {
            await Assert.ThrowsAsync<ArgumentException>(() => path.Update(
                progress: null,
                token: CancellationToken.None,
                info: info));
        }
        finally {
            path.Release();
        }
        Assert.That(File.ReadAllBytes(archivePath), Is.EqualTo(originalArchive));
    }

    [Test]
    public async Task EntryInfo_ListsTheFilesOfTheArchive() {
        var archivePath = await CreateArchive("test.7z", ("first.txt", "first"), ("second.txt", "second"));
        var path = Open(archivePath);
        try {
            var items = await path.EntryInfo(CancellationToken.None);
            var names = items.Items
                .Where(item => item.Kind == SevenZipEntryKind.File)
                .Select(item => item.Name.Original)
                .ToList();
            Assert.That(names, Is.EquivalentTo(new[] { "first.txt", "second.txt" }));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task EntryInfo_AddsTheFoldersThatTheArchiveDoesNotHold() {
        var archivePath = await CreateArchive("test.7z", ("folder/first.txt", "first"));
        var path = Open(archivePath);
        try {
            var items = await path.EntryInfo(CancellationToken.None);
            var folders = items.Items
                .Where(item => item.Kind == SevenZipEntryKind.Path)
                .Select(item => item.Name.Original)
                .ToList();
            Assert.That(folders, Is.EquivalentTo(new[] { "folder" }));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task EntryInfo_AddsParentsForDirectoriesWithoutParentEntries() {
        var archivePath = CreateArchiveWithDirectory("test.7z", "parent/child");
        var path = Open(archivePath);
        try {
            var items = await path.EntryInfo(CancellationToken.None);
            var paths = items.Items
                .Where(item => item.Kind == SevenZipEntryKind.Path)
                .Select(item => item.Name.Normalized)
                .ToList();
            Assert.That(paths, Is.EquivalentTo(new[] {
                "parent",
                Path.Combine("parent", "child")
            }));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task EntryInfo_HoldsTheSizeOfTheFiles() {
        var archivePath = await CreateArchive("test.7z", ("first.txt", "first"));
        var path = Open(archivePath);
        try {
            var items = await path.EntryInfo(CancellationToken.None);
            var file = items.Items.Single(item => item.Kind == SevenZipEntryKind.File);
            Assert.That(file.SizeOriginal, Is.EqualTo(5));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Get_ReturnsTheSamePathForTheSameFile() {
        var archivePath = await CreateArchive("test.7z", ("first.txt", "first"));
        var first = Open(archivePath);
        var second = Open(archivePath);
        try {
            Assert.That(second, Is.SameAs(first));
        }
        finally {
            second.Release();
            first.Release();
        }
    }

    [Test]
    public async Task Get_ReturnsTheSamePathForCaseVariantPathsToTheSameFile() {
        var archivePath = await CreateArchive("test.7z", ("first.txt", "first"));
        var caseVariant = Path.Combine(
            Path.GetDirectoryName(archivePath),
            Path.GetFileName(archivePath).ToUpperInvariant());
        if (File.Exists(caseVariant) == false) {
            Assert.Ignore("The test directory is case-sensitive.");
        }
        var first = Open(archivePath);
        var second = Open(caseVariant);
        try {
            Assert.That(second, Is.SameAs(first));
        }
        finally {
            second.Release();
            first.Release();
        }
    }

    [Test]
    public async Task GetAsync_ReturnsTheSamePathAsTheSyncFallbackForCaseVariantPathsToTheSameFile() {
        var archivePath = await CreateArchive("test.7z", ("first.txt", "first"));
        var caseVariant = Path.Combine(
            Path.GetDirectoryName(archivePath),
            Path.GetFileName(archivePath).ToUpperInvariant());
        if (File.Exists(caseVariant) == false) {
            Assert.Ignore("The test directory is case-sensitive.");
        }
        var first = SevenZipArchivePath.GetForTest(new FileInfo(archivePath));
        var second = await SevenZipArchivePath.GetAsync(new FileInfo(caseVariant), CancellationToken.None);
        try {
            Assert.That(second, Is.SameAs(first));
        }
        finally {
            second.Release();
            first.Release();
        }
    }

    [Test]
    public void Get_ReturnsDistinctPathsForCaseVariantFilesInCaseSensitiveDirectory() {
        var directory = Sandbox.CreateDirectory("case-sensitive");
        if (SevenZipCaseSensitiveDirectory.TryEnable(directory) == false) {
            Assert.Ignore("The file system does not support case-sensitive directories here.");
        }
        var lowerPath = Path.Combine(directory, "archive.7z");
        var upperPath = Path.Combine(directory, "ARCHIVE.7Z");
        File.WriteAllText(lowerPath, "lower");
        File.WriteAllText(upperPath, "upper");
        var lower = Open(lowerPath);
        var upper = Open(upperPath);
        try {
            Assert.That(upper, Is.Not.SameAs(lower));
        }
        finally {
            upper.Release();
            lower.Release();
        }
    }

    [Test]
    public async Task Update_AddsTheFileToTheArchive() {
        var archivePath = await CreateArchive("test.7z", ("first.txt", "first"));
        var path = Open(archivePath);
        try {
            await path.Update(progress: null, token: CancellationToken.None, info: new() {
                CreateEntriesFromFiles = new Dictionary<string, string> {
                    { "second.txt", Sandbox.WriteFile("second.txt", "second") }
                }
            });
            var items = await path.EntryInfo(CancellationToken.None);
            Assert.That(items.Items.Select(item => item.Name.Original),
                Is.EquivalentTo(new[] { "first.txt", "second.txt" }));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_RemovesTheItemFromTheArchive() {
        var archivePath = await CreateArchive("test.7z", ("first.txt", "first"), ("second.txt", "second"));
        var path = Open(archivePath);
        try {
            await path.Update(progress: null, token: CancellationToken.None, info: new() {
                DeleteEntries = new[] { "first.txt" }
            });
            var items = await path.EntryInfo(CancellationToken.None);
            Assert.That(items.Items.Select(item => item.Name.Original),
                Is.EquivalentTo(new[] { "second.txt" }));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_RemovesTheItemsOfTheFolderThatIsRemoved() {
        var archivePath = await CreateArchive("test.7z", ("folder/first.txt", "first"), ("second.txt", "second"));
        var path = Open(archivePath);
        try {
            await path.Update(progress: null, token: CancellationToken.None, info: new() {
                DeleteEntries = new[] { "folder" }
            });
            var items = await path.EntryInfo(CancellationToken.None);
            Assert.That(items.Items.Select(item => item.Name.Original),
                Is.EquivalentTo(new[] { "second.txt" }));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_RemovingDirectoryPreservesDifferentlyCasedSiblingContent() {
        var archivePath = Sandbox.PathFor("test.7z");
        var upperSource = Sandbox.WriteFile("upper-source.txt", "upper");
        var lowerSource = Sandbox.WriteFile("lower-source.txt", "lower");
        await SevenZipArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "Foo/a.txt", upperSource },
            { "foo/b.txt", lowerSource }
        }, null, null, CancellationToken.None);
        var path = Open(archivePath);
        try {
            await path.Update(progress: null, token: CancellationToken.None, info: new() {
                DeleteEntries = new[] { "Foo" }
            });
            var items = await path.EntryInfo(CancellationToken.None);
            var remaining = items.Items.Single(item => item.Kind == SevenZipEntryKind.File);
            var destination = Sandbox.PathFor("remaining.txt");
            await remaining.Extract(destination, overwrite: false, CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(remaining.Name.Original, Is.EqualTo("foo/b.txt"));
                Assert.That(File.ReadAllText(destination), Is.EqualTo("lower"));
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_StreamWithAbsolutePathDoesNotOverwriteOrDeleteOwnedFile() {
        var archivePath = await CreateArchive("target.7z", ("existing.txt", "existing"));
        var sentinel = Sandbox.WriteFile("absolute-sentinel.txt", "owned");
        await AssertUpdateRejected(archivePath, new() {
            CopyStreamSets = new[] {
                new TestEntryStreamSet(Path.GetFullPath(sentinel), "replacement")
            }
        });
        Assert.That(File.Exists(sentinel), Is.True);
        Assert.That(File.ReadAllText(sentinel), Is.EqualTo("owned"));
    }

    [Test]
    public async Task Update_StreamWithParentRelativePathDoesNotOverwriteOrDeleteOwnedFile() {
        var archivePath = await CreateArchive("target.7z", ("existing.txt", "existing"));
        var sentinel = Sandbox.WriteFile("parent-sentinel.txt", "owned");
        var stagingDirectory = Path.Combine(Path.GetTempPath(), "Brows", "SevenZip", "staging");
        var relativePath = Path.GetRelativePath(stagingDirectory, sentinel)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        await AssertUpdateRejected(archivePath, new() {
            CopyStreamSets = new[] {
                new TestEntryStreamSet(relativePath, "replacement")
            }
        });
        Assert.That(File.Exists(sentinel), Is.True);
        Assert.That(File.ReadAllText(sentinel), Is.EqualTo("owned"));
    }

    [Test]
    public async Task Update_FileTargetsWithRootedOrParentRelativeNamesAreRejected() {
        var archivePath = await CreateArchive("target.7z", ("existing.txt", "existing"));
        var source = Sandbox.WriteFile("file-source.txt", "replacement");
        var absoluteName = Path.GetFullPath(Sandbox.PathFor("absolute-entry.txt"));
        await AssertUpdateRejected(archivePath, new() {
            CreateEntriesFromFiles = new Dictionary<string, string> {
                { absoluteName, source }
            }
        });
        await AssertUpdateRejected(archivePath, new() {
            CreateEntriesFromFiles = new Dictionary<string, string> {
                { "../parent-entry.txt", source }
            }
        });
    }

    [Test]
    public async Task Update_StreamCopiesOrdinaryAndNestedRelativePaths() {
        var archivePath = await CreateArchive("target.7z", ("existing.txt", "existing"));
        var path = Open(archivePath);
        try {
            await path.Update(progress: null, token: CancellationToken.None, info: new() {
                CopyStreamSets = new[] {
                    new TestEntryStreamSet("ordinary.txt", "ordinary"),
                    new TestEntryStreamSet("nested/folder.txt", "nested")
                }
            });
            var items = await path.EntryInfo(CancellationToken.None);
            var ordinary = items.Items.Single(item => item.Name.Original == "ordinary.txt");
            var nested = items.Items.Single(item => item.Name.Original == "nested/folder.txt");
            var ordinaryDestination = Sandbox.PathFor("ordinary-result.txt");
            var nestedDestination = Sandbox.PathFor("nested-result.txt");
            await ordinary.Extract(ordinaryDestination, overwrite: false, CancellationToken.None);
            await nested.Extract(nestedDestination, overwrite: false, CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(File.ReadAllText(ordinaryDestination), Is.EqualTo("ordinary"));
                Assert.That(File.ReadAllText(nestedDestination), Is.EqualTo("nested"));
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_CopiesFromTheSameArchiveWithoutWaitingOnItsOwnLock() {
        var archivePath = await CreateArchive("same.7z", ("first.txt", "first"));
        var path = Open(archivePath);
        var source = await CreateArchiveStreamSource(archivePath, "first.txt");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try {
            await path.Update(progress: null, token: deadline.Token, info: new() {
                CopyStreamSets = new[] { new SevenZipStreamSet(new[] { source.Source }) }
            }).WaitAsync(TimeSpan.FromSeconds(7));
            using var archive = SevenZipArchive.Open(archivePath);
            Assert.That(archive.Entries.Select(entry => entry.Path), Does.Contain("first.txt"));
        }
        finally {
            source.Archive.Release();
            path.Release();
        }
    }

    [Test]
    public async Task Update_CopiesBetweenTwoArchivesWithoutOppositeLockDeadlock() {
        var archiveAPath = await CreateArchive("a.7z", ("from-a.txt", "a"));
        var archiveBPath = await CreateArchive("b.7z", ("from-b.txt", "b"));
        var pathA = Open(archiveAPath);
        var pathB = Open(archiveBPath);
        var sourceA = await CreateArchiveStreamSource(archiveAPath, "from-a.txt");
        var sourceB = await CreateArchiveStreamSource(archiveBPath, "from-b.txt");
        var rendezvous = new CopyRendezvous(2);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try {
            var copyToA = pathA.Update(progress: null, token: deadline.Token, info: new() {
                CopyStreamSets = new[] {
                    new SevenZipStreamSet(new[] {
                        new RendezvousEntryStreamSource(sourceB.Source, rendezvous)
                    })
                }
            });
            var copyToB = pathB.Update(progress: null, token: deadline.Token, info: new() {
                CopyStreamSets = new[] {
                    new SevenZipStreamSet(new[] {
                        new RendezvousEntryStreamSource(sourceA.Source, rendezvous)
                    })
                }
            });
            await Task.WhenAll(copyToA, copyToB).WaitAsync(TimeSpan.FromSeconds(7));
            using var archiveA = SevenZipArchive.Open(archiveAPath);
            using var archiveB = SevenZipArchive.Open(archiveBPath);
            using (Assert.EnterMultipleScope()) {
                Assert.That(archiveA.Entries.Select(entry => entry.Path), Does.Contain("from-b.txt"));
                Assert.That(archiveB.Entries.Select(entry => entry.Path), Does.Contain("from-a.txt"));
            }
        }
        finally {
            sourceB.Archive.Release();
            sourceA.Archive.Release();
            pathB.Release();
            pathA.Release();
        }
    }

    [Test]
    public async Task Update_CopiesAnEmptyDirectoryStreamAsAnArchiveDirectory() {
        var archivePath = Sandbox.PathFor("directory-target.7z");
        var path = Open(archivePath);
        try {
            await path.Update(progress: null, token: CancellationToken.None, info: new() {
                CopyStreamSets = new[] {
                    new TestEntryStreamSet("empty", "", isDirectory: true)
                }
            });
            using var archive = SevenZipArchive.Open(archivePath);
            var entry = archive.Entries.Single();
            using (Assert.EnterMultipleScope()) {
                Assert.That(entry.Path, Is.EqualTo("empty"));
                Assert.That(entry.IsDirectory, Is.True);
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_CreatesAnArchiveDirectoryFromAFilesystemDirectory() {
        var archivePath = Sandbox.PathFor("filesystem-directory.7z");
        var directory = Sandbox.CreateDirectory("source-empty");
        var path = Open(archivePath);
        try {
            await path.Update(progress: null, token: CancellationToken.None, info: new() {
                CreateEntriesFromFiles = new Dictionary<string, string> {
                    { "empty", directory }
                }
            });
            using var archive = SevenZipArchive.Open(archivePath);
            var entry = archive.Entries.Single();
            using (Assert.EnterMultipleScope()) {
                Assert.That(entry.Path, Is.EqualTo("empty"));
                Assert.That(entry.IsDirectory, Is.True);
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task StreamSource_ExposesAnEmptyDirectoryForFileSystemCopy() {
        var archivePath = CreateArchiveWithDirectory("directory-source.7z", "empty");
        var factory = new SevenZipProviderFactory();
        var archive = await SevenZipID.Parse(archivePath, factory, CancellationToken.None);
        try {
            var provider = new SevenZipProvider(archive, factory);
            var info = (await archive.EntryInfo(CancellationToken.None))
                .Single(item => item.Kind == SevenZipEntryKind.Path);
            IEntryStreamSource source = new SevenZipStreamSource(new SevenZipEntry(provider, info));
            using (Assert.EnterMultipleScope()) {
                Assert.That(source.RelativePath, Is.EqualTo("empty"));
                Assert.That(source.SourceDirectory, Is.EqualTo("empty"));
            }
        }
        finally {
            archive.Release();
        }
    }

    [Test]
    public async Task Update_StagesStreamOnDiskBeforeReadingItToCompletion() {
        var archivePath = Sandbox.PathFor("stream-target.7z");
        var path = Open(archivePath);
        var source = new BlockingEntryStreamSource("large.bin");
        var existing = StagingDirectories();
        var update = path.Update(progress: null, token: CancellationToken.None, info: new() {
            CopyStreamSets = new[] { new BlockingEntryStreamSet(source) }
        });
        try {
            await source.FirstRead.WaitAsync(TimeSpan.FromSeconds(5));
            var staged = StagingDirectories().Except(existing, StringComparer.OrdinalIgnoreCase);
            Assert.That(staged, Is.Not.Empty);
        }
        finally {
            source.AllowCompletion();
            await update.WaitAsync(TimeSpan.FromSeconds(5));
            path.Release();
        }
    }

    [Test]
    public async Task StreamReady_RejectsParentTraversalInArchiveName() {
        var name = "../entry.txt";
        var archivePath = CreateArchiveWithEntryName(name);
        var factory = new SevenZipProviderFactory();
        var archive = await SevenZipID.Parse(archivePath, factory, CancellationToken.None);
        try {
            var provider = new SevenZipProvider(archive, factory);
            var items = await archive.ArchivePath.EntryInfo(CancellationToken.None);
            var info = items.Items.Single(item => item.Name.Original == name);
            IEntryStreamSource source = new SevenZipStreamSource(new SevenZipEntry(provider, info));
            IEntryStreamReady ready = null;
            try {
                await Assert.ThrowsAsync<ArgumentException>(async () => {
                    ready = await source.StreamReady(CancellationToken.None);
                });
            }
            finally {
                ready?.Dispose();
            }
        }
        finally {
            archive.Release();
        }
    }

    [Test]
    public async Task StreamReady_RemovesItsStagingDirectoryWhenExtractionFails() {
        var archivePath = await CreateArchive("source.7z", ("source.txt", "content"));
        var (archive, source) = await CreateArchiveStreamSource(archivePath, "source.txt");
        try {
            File.Delete(archivePath);
            var existing = StagingDirectories();
            await Assert.CatchAsync<Exception>(async () => {
                using var ready = await source.StreamReady(CancellationToken.None);
            });
            Assert.That(StagingDirectories(), Is.EquivalentTo(existing));
        }
        finally {
            archive.Release();
        }
    }

    [Test]
    public async Task StreamReady_RejectsDriveRelativeArchiveName() {
        var root = Path.GetPathRoot(Sandbox.Root) ?? string.Empty;
        var hasDriveRoot = root.Length >= 2 && root[1] == ':';
        if (hasDriveRoot == false) {
            Assert.Ignore("This test requires a drive-letter temporary folder.");
        }
        var name = root.Substring(0, 2) + "owned.txt";
        var archivePath = CreateArchiveWithEntryName(name);
        var factory = new SevenZipProviderFactory();
        var archive = await SevenZipID.Parse(archivePath, factory, CancellationToken.None);
        try {
            var provider = new SevenZipProvider(archive, factory);
            var items = await archive.ArchivePath.EntryInfo(CancellationToken.None);
            var info = items.Items.Single(item => item.Name.Original == name);
            IEntryStreamSource source = new SevenZipStreamSource(new SevenZipEntry(provider, info));
            IEntryStreamReady ready = null;
            try {
                await Assert.ThrowsAsync<ArgumentException>(async () => {
                    ready = await source.StreamReady(CancellationToken.None);
                });
            }
            finally {
                ready?.Dispose();
            }
        }
        finally {
            archive.Release();
        }
    }

    [Test]
    public async Task Update_KeepsTheContentOfTheItemsThatAreKept() {
        var archivePath = await CreateArchive("test.7z", ("first.txt", "first"), ("second.txt", "second"));
        var path = Open(archivePath);
        try {
            await path.Update(progress: null, token: CancellationToken.None, info: new() {
                DeleteEntries = new[] { "second.txt" }
            });
            var directory = Sandbox.CreateDirectory("extracted");
            await path.ExtractAll(directory, false, null, CancellationToken.None);
            Assert.That(File.ReadAllText(Path.Combine(directory, "first.txt")), Is.EqualTo("first"));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_ReportsFileSizeProgressForArchiveWork() {
        var archivePath = await CreateArchive("test.7z", ("first.txt", "first"), ("second.txt", "second"));
        var path = Open(archivePath);
        try {
            var progress = new SevenZipTestProgress();
            await path.Update(progress: progress, token: CancellationToken.None, info: new() {
                DeleteEntries = new[] { "second.txt" },
                CreateEntriesFromFiles = new Dictionary<string, string> {
                    { "third.txt", Sandbox.WriteFile("third.txt", "third") }
                }
            });
            using (Assert.EnterMultipleScope()) {
                Assert.That(progress.Kind, Is.EqualTo(OperationProgressKind.FileSize));
                Assert.That(progress.Target, Is.GreaterThan(2));
                Assert.That(progress.Value, Is.GreaterThan(2));
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Read_ExtractsTheItemToTheGivenFile() {
        var archivePath = await CreateArchive("test.7z", ("first.txt", "first"));
        var path = Open(archivePath);
        try {
            var destination = Sandbox.PathFor("extracted.txt");
            var progress = new SevenZipTestProgress();
            await path.Read(info: new() {
                ExtractEntriesToFiles = new Dictionary<string, string> {
                    { "first.txt", destination }
                }
            }, progress: progress, token: CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(File.ReadAllText(destination), Is.EqualTo("first"));
                Assert.That(progress.Kind, Is.EqualTo(OperationProgressKind.FileSize));
                Assert.That(progress.Target, Is.GreaterThan(0));
                Assert.That(progress.Value, Is.GreaterThan(0));
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task RunArchiveOperation_WaitsForAsyncWorkToFinish() {
        var workStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWork = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = SevenZipArchivePath.RunArchiveOperation(
            "archive.7z",
            new SevenZipTestProgress(),
            async (_, token) => {
                workStarted.SetResult(true);
                await releaseWork.Task.WaitAsync(token);
            },
            CancellationToken.None);

        try {
            await workStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<TimeoutException>(() => operation.WaitAsync(TimeSpan.FromMilliseconds(100)));
        }
        finally {
            releaseWork.TrySetResult(true);
        }

        await operation;
    }

    [Test]
    public async Task ExtractAll_WritesEveryItemOfTheArchiveToTheFolder() {
        var archivePath = await CreateArchive("test.7z", ("first.txt", "first"), ("folder/second.txt", "second"));
        var path = Open(archivePath);
        try {
            var directory = Sandbox.CreateDirectory("extracted");
            await path.ExtractAll(directory, false, null, CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(File.ReadAllText(Path.Combine(directory, "first.txt")), Is.EqualTo("first"));
                Assert.That(File.ReadAllText(Path.Combine(directory, "folder", "second.txt")), Is.EqualTo("second"));
            }
        }
        finally {
            path.Release();
        }
    }

    private async Task<(string ArchiveFile, string Inner)> CreateArchiveInsideArchive() {
        var innerPath = await CreateArchive("inner.7z", ("first.txt", "first"), ("folder/second.txt", "second"));
        var archivePath = Sandbox.PathFor("test.7z");
        await SevenZipArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "inner.7z", innerPath }
        }, null, null, CancellationToken.None);
        return (archivePath, "inner.7z");
    }

    [Test]
    public async Task NestedOperations_RemoveStagingDirectoriesAfterSuccess() {
        var (archiveFile, inner) = await CreateArchiveInsideArchive();
        var path = OpenNested(archiveFile, inner);
        var existing = StagingDirectories();
        try {
            await path.EntryInfo(CancellationToken.None);
            Assert.That(StagingDirectories(), Is.EquivalentTo(existing));

            var readDestination = Sandbox.PathFor("nested-read.txt");
            await path.Read(progress: null, token: CancellationToken.None, info: new() {
                ExtractEntriesToFiles = new Dictionary<string, string> {
                    { "folder/second.txt", readDestination }
                }
            });
            Assert.That(File.ReadAllText(readDestination), Is.EqualTo("second"));
            Assert.That(StagingDirectories(), Is.EquivalentTo(existing));

            var extractDirectory = Sandbox.CreateDirectory("nested-extract");
            await path.ExtractAll(extractDirectory, overwrite: false, progress: null, CancellationToken.None);
            Assert.That(File.ReadAllText(Path.Combine(extractDirectory, "first.txt")), Is.EqualTo("first"));
            Assert.That(StagingDirectories(), Is.EquivalentTo(existing));

            await path.Update(progress: null, token: CancellationToken.None, info: new() {
                CreateEntriesFromFiles = new Dictionary<string, string> {
                    { "third.txt", Sandbox.WriteFile("third.txt", "third") }
                }
            });
            Assert.That(StagingDirectories(), Is.EquivalentTo(existing));
        }
        finally {
            path.Release();
            RemoveNewStagingDirectories(existing);
        }
    }

    [Test]
    public async Task EntryInfo_RemovesStagingDirectoriesWhenNestedArchiveCannotBeOpened() {
        var archiveFile = await CreateArchive("invalid-outer.7z", ("inner.7z", "not an archive"));
        var path = OpenNested(archiveFile, "inner.7z");
        var existing = StagingDirectories();
        try {
            await Assert.CatchAsync<Exception>(() => path.EntryInfo(CancellationToken.None));
            Assert.That(StagingDirectories(), Is.EquivalentTo(existing));
        }
        finally {
            path.Release();
            RemoveNewStagingDirectories(existing);
        }
    }

    [Test]
    public async Task EntryInfo_ListsTheItemsOfAnArchiveThatIsInsideTheArchive() {
        var (archiveFile, inner) = await CreateArchiveInsideArchive();
        var path = OpenNested(archiveFile, inner);
        try {
            var items = await path.EntryInfo(CancellationToken.None);
            var names = items.Items
                .Where(item => item.Kind == SevenZipEntryKind.File)
                .Select(item => item.Name.Original)
                .ToList();
            Assert.That(names, Is.EquivalentTo(new[] { "first.txt", "folder/second.txt" }));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Read_ExtractsTheItemOfAnArchiveThatIsInsideTheArchive() {
        var (archiveFile, inner) = await CreateArchiveInsideArchive();
        var path = OpenNested(archiveFile, inner);
        try {
            var destination = Sandbox.PathFor("extracted.txt");
            await path.Read(progress: null, token: CancellationToken.None, info: new() {
                ExtractEntriesToFiles = new Dictionary<string, string> {
                    { "folder/second.txt", destination }
                }
            });
            Assert.That(File.ReadAllText(destination), Is.EqualTo("second"));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_AddsTheItemToTheArchiveThatIsInsideTheArchive() {
        var (archiveFile, inner) = await CreateArchiveInsideArchive();
        var path = OpenNested(archiveFile, inner);
        try {
            await path.Update(progress: null, token: CancellationToken.None, info: new() {
                CreateEntriesFromFiles = new Dictionary<string, string> {
                    { "third.txt", Sandbox.WriteFile("third.txt", "third") }
                }
            });
            using (var outer = SevenZipArchive.Open(archiveFile)) {
                var innerIndex = outer.IndexOf(inner);
                Assert.That(innerIndex, Is.GreaterThanOrEqualTo(0));
                var extraction = Sandbox.PathFor("extracted-inner.7z");
                await Task.Run(
                    () => outer.Extract(innerIndex, extraction, overwrite: true, null, CancellationToken.None),
                    CancellationToken.None);
                using (var innerArchive = SevenZipArchive.Open(extraction)) {
                    Assert.That(innerArchive.Entries.Select(entry => entry.Path),
                        Is.EquivalentTo(new[] { "first.txt", "folder/second.txt", "third.txt" }));
                }
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_RemovesTheItemFromTheArchiveThatIsInsideTheArchive() {
        var (archiveFile, inner) = await CreateArchiveInsideArchive();
        var path = OpenNested(archiveFile, inner);
        try {
            await path.Update(progress: null, token: CancellationToken.None, info: new() {
                DeleteEntries = new[] { "first.txt" }
            });
            using (var outer = SevenZipArchive.Open(archiveFile)) {
                var innerIndex = outer.IndexOf(inner);
                Assert.That(innerIndex, Is.GreaterThanOrEqualTo(0));
                var extraction = Sandbox.PathFor("extracted-inner.7z");
                await Task.Run(
                    () => outer.Extract(innerIndex, extraction, overwrite: true, null, CancellationToken.None),
                    CancellationToken.None);
                using (var innerArchive = SevenZipArchive.Open(extraction)) {
                    Assert.That(innerArchive.Entries.Select(entry => entry.Path),
                        Is.EquivalentTo(new[] { "folder/second.txt" }));
                }
            }
        }
        finally {
            path.Release();
        }
    }

    private sealed class TestEntryStreamSet : IEntryStreamSet {
        private readonly TestEntryStreamSource Source;

        public TestEntryStreamSet(string relativePath, string contents, bool isDirectory = false) {
            Source = new TestEntryStreamSource(relativePath, contents, isDirectory);
        }

        public IEntryStreamReady StreamSourceReady() {
            return new EntryStreamReady();
        }

        public IEnumerable<string> FileSource() {
            return Array.Empty<string>();
        }

        public async IAsyncEnumerable<IEntryStreamSource> StreamSource(
            [EnumeratorCancellation] CancellationToken token) {
            token.ThrowIfCancellationRequested();
            yield return Source;
            await Task.CompletedTask;
        }
    }

    private sealed class TestEntryStreamSource : IEntryStreamSource {
        private readonly byte[] Contents;

        private readonly bool IsDirectory;

        public TestEntryStreamSource(string relativePath, string contents, bool isDirectory) {
            RelativePath = relativePath;
            Contents = Encoding.UTF8.GetBytes(contents);
            IsDirectory = isDirectory;
        }

        public string EntryID => "test";
        public string EntryName => RelativePath;
        public string RelativePath { get; }
        public string SourceFile => null;
        public string SourceDirectory => IsDirectory ? RelativePath : null;
        public long StreamLength => Contents.Length;
        public Stream Stream { get; private set; }

        public Task<IEntryStreamReady> StreamReady(CancellationToken token) {
            token.ThrowIfCancellationRequested();
            if (IsDirectory == false) {
                Stream = new MemoryStream(Contents, writable: false);
            }
            return Task.FromResult<IEntryStreamReady>(new EntryStreamReady());
        }
    }

    private sealed class BlockingEntryStreamSet : IEntryStreamSet {
        private readonly BlockingEntryStreamSource Source;

        public BlockingEntryStreamSet(BlockingEntryStreamSource source) {
            Source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public IEntryStreamReady StreamSourceReady() {
            return new EntryStreamReady();
        }

        public IEnumerable<string> FileSource() {
            return Array.Empty<string>();
        }

        public async IAsyncEnumerable<IEntryStreamSource> StreamSource(
            [EnumeratorCancellation] CancellationToken token) {
            token.ThrowIfCancellationRequested();
            yield return Source;
            await Task.CompletedTask;
        }
    }

    private sealed class CopyRendezvous {
        private readonly int ParticipantCount;
        private readonly TaskCompletionSource<bool> AllArrived = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int Arrived;

        public CopyRendezvous(int participantCount) {
            ParticipantCount = participantCount;
        }

        public async Task Wait(CancellationToken token) {
            if (Interlocked.Increment(ref Arrived) == ParticipantCount) {
                AllArrived.TrySetResult(true);
            }
            await AllArrived.Task.WaitAsync(token);
        }
    }

    private sealed class RendezvousEntryStreamSource : IEntryStreamSource {
        private readonly IEntryStreamSource Source;
        private readonly CopyRendezvous Rendezvous;

        public string EntryID => Source.EntryID;
        public string EntryName => Source.EntryName;
        public string RelativePath => Source.RelativePath;
        public string SourceFile => Source.SourceFile;
        public string SourceDirectory => Source.SourceDirectory;
        public long StreamLength => Source.StreamLength;
        public Stream Stream => Source.Stream;

        public RendezvousEntryStreamSource(IEntryStreamSource source, CopyRendezvous rendezvous) {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Rendezvous = rendezvous ?? throw new ArgumentNullException(nameof(rendezvous));
        }

        public async Task<IEntryStreamReady> StreamReady(CancellationToken token) {
            await Rendezvous.Wait(token);
            return await Source.StreamReady(token);
        }
    }

    private sealed class BlockingEntryStreamSource : IEntryStreamSource {
        private readonly TaskCompletionSource<bool> FirstReadSource = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> CompletionSource = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Stream Content = new BlockingReadStream();

        public Task FirstRead => FirstReadSource.Task;

        public string EntryID => "blocking";
        public string EntryName => RelativePath;
        public string RelativePath { get; }
        public string SourceFile => null;
        public string SourceDirectory => null;
        public long StreamLength => long.MaxValue;
        public Stream Stream => Content;

        public BlockingEntryStreamSource(string relativePath) {
            RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
            ((BlockingReadStream)Content).FirstRead = FirstReadSource;
            ((BlockingReadStream)Content).Completion = CompletionSource;
        }

        public Task<IEntryStreamReady> StreamReady(CancellationToken token) {
            token.ThrowIfCancellationRequested();
            return Task.FromResult<IEntryStreamReady>(new EntryStreamReady());
        }

        public void AllowCompletion() {
            CompletionSource.TrySetResult(true);
        }
    }

    private sealed class BlockingReadStream : Stream {
        private static readonly byte[] Payload = Encoding.UTF8.GetBytes("stream payload");
        private int ReadCount;

        public TaskCompletionSource<bool> FirstRead { get; set; }
        public TaskCompletionSource<bool> Completion { get; set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() {
        }

        public override int Read(byte[] buffer, int offset, int count) {
            if (Interlocked.Increment(ref ReadCount) == 1) {
                var length = Math.Min(count, Payload.Length);
                Array.Copy(Payload, 0, buffer, offset, length);
                FirstRead.TrySetResult(true);
                return length;
            }
            Completion.Task.GetAwaiter().GetResult();
            return 0;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) {
            if (Interlocked.Increment(ref ReadCount) == 1) {
                var length = Math.Min(buffer.Length, Payload.Length);
                Payload.AsMemory(0, length).CopyTo(buffer);
                FirstRead.TrySetResult(true);
                return length;
            }
            await Completion.Task.WaitAsync(cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) {
            return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
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
