using Brows.Entries;
using Brows.Operations;
using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TarFsEntry = System.Formats.Tar.TarEntry;

namespace Brows;

/// <summary>
/// Tests the archive of the plugin. The tests use real archives that are
/// created and removed on the real file system.
/// </summary>
[TestFixture]
internal sealed partial class TarArchivePathTest {
    private TarTestSandbox Sandbox;

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
        await TarArchiveFile.Create(archivePath, files.ToDictionary(
            file => file.Path,
            file => Sandbox.WriteFile(file.Path, file.Contents)), null, CancellationToken.None);
        return archivePath;
    }

    private async Task<string> CreateArchive(string name, IReadOnlyDictionary<string, string> files) {
        var archivePath = Sandbox.PathFor(name);
        await TarArchiveFile.Create(archivePath, files, null, CancellationToken.None);
        return archivePath;
    }

    /// <summary>
    /// Creates an archive that holds the given entries, without the name
    /// checks that an archive update applies.
    /// </summary>
    private static void CreateRawArchive(string archivePath, params (string Path, TarEntryType Kind, string Contents)[] entries) {
        using (var stream = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
            using var writer = new TarWriter(stream);
            foreach (var entry in entries) {
                var tarEntry = entry.Kind switch {
                    TarEntryType.Directory => new PaxTarEntry(TarEntryType.Directory, entry.Path) as TarFsEntry,
                    _ => new PaxTarEntry(TarEntryType.RegularFile, entry.Path),
                };
                if (entry.Kind == TarEntryType.RegularFile) {
                    tarEntry.DataStream = new MemoryStream(Encoding.UTF8.GetBytes(entry.Contents), writable: false);
                }
                writer.WriteEntry(tarEntry);
            }
        }
    }

    private async Task<string> CreateArchiveWithDirectory(string name, string directoryName) {
        var archivePath = Sandbox.PathFor(name);
        await TarArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { directoryName, Sandbox.CreateDirectory("source-directory") }
        }, null, CancellationToken.None);
        return archivePath;
    }

    private async Task<(TarID Archive, IEntryStreamSource Source)> CreateArchiveStreamSource(
        string archivePath,
        string entryName) {
        var factory = new TarProviderFactory();
        var archive = await TarID.Parse(archivePath, factory, CancellationToken.None)
            ?? throw new InvalidOperationException("The source archive could not be opened.");
        try {
            var provider = new TarProvider(archive, factory);
            var info = (await archive.EntryInfo(CancellationToken.None))
                .Single(item => item.Name.Original == entryName);
            var source = new TarStreamSource(new TarEntry(provider, info));
            return (archive, source);
        }
        catch {
            archive.Release();
            throw;
        }
    }

    private static IReadOnlySet<string> StagingDirectories() {
        var directory = Path.Combine(Path.GetTempPath(), "Brows", "Tar");
        return Directory.Exists(directory)
            ? Directory.GetDirectories(directory).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private static void RemoveNewStagingDirectories(IReadOnlySet<string> existing) {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Brows", "Tar"));
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
                    "Refusing to remove a directory outside the Tar staging folder.");
            }
            foreach (var file in Directory.GetFiles(fullDirectory)) {
                File.Delete(file);
            }
            Directory.Delete(fullDirectory);
        }
    }

    /// <summary>
    /// Opens the archive of the given location.
    /// </summary>
    private static TarArchivePath Open(string archivePath) {
        return TarArchivePath.GetForTest(new FileInfo(archivePath));
    }

    /// <summary>
    /// Opens the nested archive that the nesting names.
    /// </summary>
    private static TarArchivePath OpenNested(string archivePath, params string[] nest) {
        var identity = TarArchiveIdentity.CreateFallback(new FileInfo(archivePath));
        return TarArchivePath.Get(identity, new TarArchiveNest(nest));
    }

    private async Task AssertUpdateRejected(string archivePath, TarArchiveUpdate info) {
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
    public async Task Get_UsesStructuredNestingForCacheIdentityAndRelease() {
        var archivePath = await CreateArchive("cache-identity.tar", ("keep.txt", "contents"));
        var identity = TarArchiveIdentity.CreateFallback(new FileInfo(archivePath));
        var first = TarArchivePath.Get(identity, new TarArchiveNest(new[] { "a>b", "c" }));
        var second = TarArchivePath.Get(identity, new TarArchiveNest(new[] { "a", "b>c" }));
        var repeated = TarArchivePath.Get(identity, new TarArchiveNest(new[] { "a", "b>c" }));
        TarArchivePath afterRelease = null;
        var firstReleased = false;

        try {
            using (Assert.EnterMultipleScope()) {
                Assert.That(first, Is.Not.SameAs(second));
                Assert.That(second, Is.SameAs(repeated));
                Assert.That(second.FullName, Is.EqualTo($"tar:1|f={identity.FullName}|n=a|n=b%3Ec|d="));
            }

            first.Release();
            firstReleased = true;
            afterRelease = TarArchivePath.Get(
                identity,
                new TarArchiveNest(new[] { "a", "b>c" }));

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

    [Test]
    public async Task EntryInfo_ListsTheFilesOfTheArchive() {
        var archivePath = await CreateArchive("test.tar", ("first.txt", "first"), ("second.txt", "second"));
        var path = Open(archivePath);
        try {
            var items = await path.EntryInfo(CancellationToken.None);
            var names = items.Items
                .Where(item => item.Kind == TarEntryKind.File)
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
        var archivePath = await CreateArchive("test.tar", ("folder/first.txt", "first"));
        var path = Open(archivePath);
        try {
            var items = await path.EntryInfo(CancellationToken.None);
            var folders = items.Items
                .Where(item => item.Kind == TarEntryKind.Path)
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
        var archivePath = await CreateArchiveWithDirectory("test.tar", "parent/child");
        var path = Open(archivePath);
        try {
            var items = await path.EntryInfo(CancellationToken.None);
            var paths = items.Items
                .Where(item => item.Kind == TarEntryKind.Path)
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
        var archivePath = await CreateArchive("test.tar", ("first.txt", "first"));
        var path = Open(archivePath);
        try {
            var items = await path.EntryInfo(CancellationToken.None);
            var file = items.Items.Single(item => item.Kind == TarEntryKind.File);
            Assert.That(file.SizeOriginal, Is.EqualTo(5));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Get_ReturnsTheSamePathForTheSameFile() {
        var archivePath = await CreateArchive("test.tar", ("first.txt", "first"));
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
        var archivePath = await CreateArchive("test.tar", ("first.txt", "first"));
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
        var archivePath = await CreateArchive("test.tar", ("first.txt", "first"));
        var caseVariant = Path.Combine(
            Path.GetDirectoryName(archivePath),
            Path.GetFileName(archivePath).ToUpperInvariant());
        if (File.Exists(caseVariant) == false) {
            Assert.Ignore("The test directory is case-sensitive.");
        }
        var first = TarArchivePath.GetForTest(new FileInfo(archivePath));
        var second = await TarArchivePath.GetAsync(new FileInfo(caseVariant), CancellationToken.None);
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
        var lowerPath = Path.Combine(directory, "archive.tar");
        var upperPath = Path.Combine(directory, "ARCHIVE.TAR");
        try {
            using (var lowerStream = new FileStream(lowerPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
            }
            using (var upperStream = new FileStream(upperPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
            }
        }
        catch (IOException) {
            Assert.Ignore("The file system does not support case-sensitive directories here.");
        }
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
        var archivePath = await CreateArchive("test.tar", ("first.txt", "first"));
        var path = Open(archivePath);
        try {
            await path.Update(progress: null, token: CancellationToken.None, info: new TarArchiveUpdate {
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
        var archivePath = await CreateArchive("test.tar", ("first.txt", "first"), ("second.txt", "second"));
        var path = Open(archivePath);
        try {
            await path.Update(progress: null, token: CancellationToken.None, info: new TarArchiveUpdate {
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
        var archivePath = await CreateArchive("test.tar", ("folder/first.txt", "first"), ("second.txt", "second"));
        var path = Open(archivePath);
        try {
            await path.Update(progress: null, token: CancellationToken.None, info: new TarArchiveUpdate {
                DeleteEntries = new[] { "folder" }
            });
            var items = await path.EntryInfo(CancellationToken.None);
            var remaining = items.Items.Select(item => item.Name.Normalized).ToList();
            Assert.That(remaining, Is.EquivalentTo(new[] { "second.txt" }));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_RemovingDirectoryPreservesDifferentlyCasedSiblingContent() {
        var archivePath = Sandbox.PathFor("test.tar");
        var upperSource = Sandbox.WriteFile("upper-source.txt", "upper");
        var lowerSource = Sandbox.WriteFile("lower-source.txt", "lower");
        await TarArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "Foo/a.txt", upperSource },
            { "foo/b.txt", lowerSource }
        }, null, CancellationToken.None);
        var path = Open(archivePath);
        try {
            await path.Update(progress: null, token: CancellationToken.None, info: new TarArchiveUpdate {
                DeleteEntries = new[] { "Foo" }
            });
            var items = await path.EntryInfo(CancellationToken.None);
            var remaining = items.Items.Single(item => item.Kind == TarEntryKind.File);
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
        var archivePath = await CreateArchive("target.tar", ("existing.txt", "existing"));
        var sentinel = Sandbox.WriteFile("absolute-sentinel.txt", "owned");
        await AssertUpdateRejected(archivePath, new TarArchiveUpdate {
            CopyStreamSets = new[] {
                new TestEntryStreamSet(Path.GetFullPath(sentinel), "replacement")
            }
        });
        Assert.That(File.Exists(sentinel), Is.True);
        Assert.That(File.ReadAllText(sentinel), Is.EqualTo("owned"));
    }

    [Test]
    public async Task Update_StreamWithParentRelativePathDoesNotOverwriteOrDeleteOwnedFile() {
        var archivePath = await CreateArchive("target.tar", ("existing.txt", "existing"));
        var sentinel = Sandbox.WriteFile("parent-sentinel.txt", "owned");
        var stagingDirectory = Path.Combine(Path.GetTempPath(), "Brows", "Tar", "staging");
        var relativePath = Path.GetRelativePath(stagingDirectory, sentinel)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        await AssertUpdateRejected(archivePath, new TarArchiveUpdate {
            CopyStreamSets = new[] {
                new TestEntryStreamSet(relativePath, "replacement")
            }
        });
        Assert.That(File.Exists(sentinel), Is.True);
        Assert.That(File.ReadAllText(sentinel), Is.EqualTo("owned"));
    }

    [Test]
    public async Task Update_FileTargetsWithRootedOrParentRelativeNamesAreRejected() {
        var archivePath = await CreateArchive("target.tar", ("existing.txt", "existing"));
        var source = Sandbox.WriteFile("file-source.txt", "replacement");
        var absoluteName = Path.GetFullPath(Sandbox.PathFor("absolute-entry.txt"));
        await AssertUpdateRejected(archivePath, new TarArchiveUpdate {
            CreateEntriesFromFiles = new Dictionary<string, string> {
                { absoluteName, source }
            }
        });
        await AssertUpdateRejected(archivePath, new TarArchiveUpdate {
            CreateEntriesFromFiles = new Dictionary<string, string> {
                { "../parent-entry.txt", source }
            }
        });
    }

    [Test]
    public async Task Update_StreamCopiesOrdinaryAndNestedRelativePaths() {
        var archivePath = await CreateArchive("target.tar", ("existing.txt", "existing"));
        var path = Open(archivePath);
        try {
            await path.Update(progress: null, token: CancellationToken.None, info: new TarArchiveUpdate {
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
        var archivePath = await CreateArchive("same.tar", ("first.txt", "first"));
        var path = Open(archivePath);
        var source = await CreateArchiveStreamSource(archivePath, "first.txt");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try {
            await path.Update(progress: null, token: deadline.Token, info: new TarArchiveUpdate {
                CopyStreamSets = new[] { new TarStreamSet(new[] { source.Source }) }
            }).WaitAsync(TimeSpan.FromSeconds(7));
            var items = await path.EntryInfo(CancellationToken.None);
            Assert.That(items.Items.Select(item => item.Name.Original),
                Is.EquivalentTo(new[] { "first.txt" }));
        }
        finally {
            source.Archive.Release();
            path.Release();
        }
    }

    [Test]
    public async Task Update_CopiesBetweenTwoArchivesWithoutOppositeLockDeadlock() {
        var archiveAPath = await CreateArchive("a.tar", ("from-a.txt", "a"));
        var archiveBPath = await CreateArchive("b.tar", ("from-b.txt", "b"));
        var pathA = Open(archiveAPath);
        var pathB = Open(archiveBPath);
        var sourceA = await CreateArchiveStreamSource(archiveAPath, "from-a.txt");
        var sourceB = await CreateArchiveStreamSource(archiveBPath, "from-b.txt");
        var rendezvous = new CopyRendezvous(2);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try {
            var copyToA = pathA.Update(progress: null, token: deadline.Token, info: new TarArchiveUpdate {
                CopyStreamSets = new[] {
                    new TarStreamSet(new[] {
                        new RendezvousEntryStreamSource(sourceB.Source, rendezvous)
                    })
                }
            });
            var copyToB = pathB.Update(progress: null, token: deadline.Token, info: new TarArchiveUpdate {
                CopyStreamSets = new[] {
                    new TarStreamSet(new[] {
                        new RendezvousEntryStreamSource(sourceA.Source, rendezvous)
                    })
                }
            });
            await Task.WhenAll(copyToA, copyToB).WaitAsync(TimeSpan.FromSeconds(7));
            var itemsA = await pathA.EntryInfo(CancellationToken.None);
            var itemsB = await pathB.EntryInfo(CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(itemsA.Items.Select(item => item.Name.Original), Does.Contain("from-b.txt"));
                Assert.That(itemsB.Items.Select(item => item.Name.Original), Does.Contain("from-a.txt"));
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
        var archivePath = Sandbox.PathFor("directory-target.tar");
        var path = Open(archivePath);
        try {
            await path.Update(progress: null, token: CancellationToken.None, info: new TarArchiveUpdate {
                CopyStreamSets = new[] {
                    new TestEntryStreamSet("empty", "", isDirectory: true)
                }
            });
            var items = await path.EntryInfo(CancellationToken.None);
            var directory = items.Items.Single(item => item.Kind == TarEntryKind.Path);
            Assert.That(directory.Name.Original, Is.EqualTo("empty"));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_CreatesAnArchiveDirectoryFromAFilesystemDirectory() {
        var archivePath = Sandbox.PathFor("filesystem-directory.tar");
        var directory = Sandbox.CreateDirectory("source-empty");
        var path = Open(archivePath);
        try {
            await path.Update(progress: null, token: CancellationToken.None, info: new TarArchiveUpdate {
                CreateEntriesFromFiles = new Dictionary<string, string> {
                    { "empty", directory }
                }
            });
            var items = await path.EntryInfo(CancellationToken.None);
            var directoryEntry = items.Items.Single(item => item.Kind == TarEntryKind.Path);
            using (Assert.EnterMultipleScope()) {
                Assert.That(directoryEntry.Name.Original, Is.EqualTo("empty"));
                Assert.That(directoryEntry.Kind, Is.EqualTo(TarEntryKind.Path));
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task StreamSource_ExposesAnEmptyDirectoryForFileSystemCopy() {
        var archivePath = await CreateArchiveWithDirectory("directory-source.tar", "empty");
        var factory = new TarProviderFactory();
        var archive = await TarID.Parse(archivePath, factory, CancellationToken.None);
        try {
            var provider = new TarProvider(archive, factory);
            var info = (await archive.EntryInfo(CancellationToken.None))
                .Single(item => item.Kind == TarEntryKind.Path);
            IEntryStreamSource source = new TarStreamSource(new TarEntry(provider, info));
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
        var archivePath = Sandbox.PathFor("stream-target.tar");
        var path = Open(archivePath);
        var source = new BlockingEntryStreamSource("large.bin");
        var existing = StagingDirectories();
        var update = path.Update(progress: null, token: CancellationToken.None, info: new TarArchiveUpdate {
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
        var archivePath = Sandbox.PathFor("traversal-source.tar");
        CreateRawArchive(archivePath, (name, TarEntryType.RegularFile, "content"));
        var factory = new TarProviderFactory();
        var archive = await TarID.Parse(archivePath, factory, CancellationToken.None);
        try {
            var provider = new TarProvider(archive, factory);
            var items = await archive.ArchivePath.EntryInfo(CancellationToken.None);
            var info = items.Items.Single(item => item.Name.Original == name);
            IEntryStreamSource source = new TarStreamSource(new TarEntry(provider, info));
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
        var archivePath = await CreateArchive("source.tar", ("source.txt", "content"));
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
        var archivePath = Sandbox.PathFor("drive-relative-source.tar");
        CreateRawArchive(archivePath, (name, TarEntryType.RegularFile, "content"));
        var factory = new TarProviderFactory();
        var archive = await TarID.Parse(archivePath, factory, CancellationToken.None);
        try {
            var provider = new TarProvider(archive, factory);
            var items = await archive.ArchivePath.EntryInfo(CancellationToken.None);
            var info = items.Items.Single(item => item.Name.Original == name);
            IEntryStreamSource source = new TarStreamSource(new TarEntry(provider, info));
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
        var archivePath = await CreateArchive("test.tar", ("first.txt", "first"), ("second.txt", "second"));
        var path = Open(archivePath);
        try {
            await path.Update(progress: null, token: CancellationToken.None, info: new TarArchiveUpdate {
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
    public async Task Update_ReportsFileSizeProgressForRetainedAndAddedEntries() {
        var archivePath = await CreateArchive("test.tar", ("first.txt", "first"), ("second.txt", "second"));
        var path = Open(archivePath);
        try {
            var progress = new TarTestProgress();
            await path.Update(progress: progress, token: CancellationToken.None, info: new TarArchiveUpdate {
                DeleteEntries = new[] { "second.txt" },
                CreateEntriesFromFiles = new Dictionary<string, string> {
                    { "third.txt", Sandbox.WriteFile("third.txt", "third") }
                }
            });
            using (Assert.EnterMultipleScope()) {
                Assert.That(progress.Kind, Is.EqualTo(OperationProgressKind.FileSize));
                Assert.That(progress.Target, Is.EqualTo(10));
                Assert.That(progress.Value, Is.EqualTo(10));
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Read_ExtractsTheItemToTheGivenFile() {
        var archivePath = await CreateArchive("test.tar", ("first.txt", "first"));
        var path = Open(archivePath);
        try {
            var destination = Sandbox.PathFor("extracted.txt");
            var progress = new TarTestProgress();
            await path.Read(info: new TarArchiveRead {
                ExtractEntriesToFiles = new Dictionary<string, string> {
                    { "first.txt", destination }
                }
            }, progress: progress, token: CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(File.ReadAllText(destination), Is.EqualTo("first"));
                Assert.That(progress.Kind, Is.EqualTo(OperationProgressKind.FileSize));
                Assert.That(progress.Target, Is.EqualTo(5));
                Assert.That(progress.Value, Is.EqualTo(5));
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Read_ReplacesTheTargetFileWhenItOverwrites() {
        var archivePath = await CreateArchive("test.tar", ("first.txt", "first"));
        var path = Open(archivePath);
        try {
            var destination = Sandbox.WriteFile("overwritten.txt", "previous");
            await path.Read(progress: null, token: CancellationToken.None, info: new TarArchiveRead {
                ExtractOverwrites = true,
                ExtractEntriesToFiles = new Dictionary<string, string> {
                    { "first.txt", destination }
                }
            });
            Assert.That(File.ReadAllText(destination), Is.EqualTo("first"));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Read_KeepsTheTargetFileWhenItDoesNotOverwrite() {
        var archivePath = await CreateArchive("test.tar", ("first.txt", "first"));
        var path = Open(archivePath);
        try {
            var destination = Sandbox.WriteFile("kept.txt", "previous");
            await Assert.ThrowsAsync<IOException>(() => path.Read(
                progress: null,
                token: CancellationToken.None,
                info: new TarArchiveRead {
                    ExtractEntriesToFiles = new Dictionary<string, string> {
                        { "first.txt", destination }
                    }
                }));
            Assert.That(File.ReadAllText(destination), Is.EqualTo("previous"));
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task ExtractAll_WritesEveryItemOfTheArchiveToTheFolder() {
        var archivePath = await CreateArchive("test.tar", ("first.txt", "first"), ("folder/second.txt", "second"));
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

    [Test]
    public async Task ExtractAll_RejectsEntryNamesThatEscapeTheDestination() {
        var archivePath = Sandbox.PathFor("traversal.tar");
        CreateRawArchive(archivePath,
            ("../escape.txt", TarEntryType.RegularFile, "escaped"),
            ("folder/../../escape2.txt", TarEntryType.RegularFile, "escaped"));
        var path = Open(archivePath);
        try {
            var directory = Sandbox.CreateDirectory("extracted");
            await Assert.ThrowsAsync<InvalidDataException>(() => path.ExtractAll(
                directory,
                overwrite: false,
                progress: null,
                token: CancellationToken.None));
            Assert.That(File.Exists(Path.Combine(Sandbox.Root, "escape.txt")), Is.False);
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task ExtractAll_WritesTheEntryModificationTimesOfTheFiles() {
        var archivePath = Sandbox.PathFor("times.tar");
        var timestamp = new DateTimeOffset(2020, 1, 2, 3, 4, 6, TimeSpan.Zero);
        using (var stream = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
            using var writer = new TarWriter(stream);
            var tarEntry = new PaxTarEntry(TarEntryType.RegularFile, "first.txt");
            tarEntry.ModificationTime = timestamp;
            tarEntry.DataStream = new MemoryStream(Encoding.UTF8.GetBytes("first"), writable: false);
            writer.WriteEntry(tarEntry);
        }
        var path = Open(archivePath);
        try {
            var destinationDirectory = Sandbox.CreateDirectory("extracted");
            await path.ExtractAll(destinationDirectory, false, null, CancellationToken.None);
            var destination = Path.Combine(destinationDirectory, "first.txt");
            Assert.That(File.GetLastWriteTimeUtc(destination), Is.EqualTo(timestamp.UtcDateTime));
        }
        finally {
            path.Release();
        }
    }

    private async Task<(string ArchiveFile, string Inner)> CreateArchiveInsideArchive() {
        var innerPath = await CreateArchive("inner.tar", ("first.txt", "first"), ("folder/second.txt", "second"));
        var archivePath = Sandbox.PathFor("test.tar");
        await TarArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "inner.tar", innerPath }
        }, null, CancellationToken.None);
        return (archivePath, "inner.tar");
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
            await path.Read(progress: null, token: CancellationToken.None, info: new TarArchiveRead {
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

            await path.Update(progress: null, token: CancellationToken.None, info: new TarArchiveUpdate {
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
        var archiveFile = await CreateArchive("invalid-outer.tar", ("inner.tar", "not an archive"));
        var path = OpenNested(archiveFile, "inner.tar");
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
                .Where(item => item.Kind == TarEntryKind.File)
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
            await path.Read(progress: null, token: CancellationToken.None, info: new TarArchiveRead {
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
        var staging = StagingDirectories();
        try {
            await path.Update(progress: null, token: CancellationToken.None, info: new TarArchiveUpdate {
                CreateEntriesFromFiles = new Dictionary<string, string> {
                    { "third.txt", Sandbox.WriteFile("third.txt", "third") }
                }
            });
            Assert.That(StagingDirectories(), Is.EquivalentTo(staging));
            var outer = Open(archiveFile);
            try {
                var innerDirectory = Sandbox.PathFor("extracted-inner");
                Directory.CreateDirectory(innerDirectory);
                await outer.ExtractAll(innerDirectory, overwrite: true, progress: null, token: CancellationToken.None);
                var innerArchive = Open(Path.Combine(innerDirectory, "inner.tar"));
                try {
                    var items = await innerArchive.EntryInfo(CancellationToken.None);
                    Assert.That(items.Items
                        .Where(item => item.Kind == TarEntryKind.File)
                        .Select(item => item.Name.Original),
                        Is.EquivalentTo(new[] { "first.txt", "folder/second.txt", "third.txt" }));
                }
                finally {
                    innerArchive.Release();
                }
            }
            finally {
                outer.Release();
            }
        }
        finally {
            path.Release();
            RemoveNewStagingDirectories(staging);
        }
    }

    [Test]
    public async Task Update_RemovesTheItemFromTheArchiveThatIsInsideTheArchive() {
        var (archiveFile, inner) = await CreateArchiveInsideArchive();
        var path = OpenNested(archiveFile, inner);
        var staging = StagingDirectories();
        try {
            await path.Update(progress: null, token: CancellationToken.None, info: new TarArchiveUpdate {
                DeleteEntries = new[] { "first.txt" }
            });
            Assert.That(StagingDirectories(), Is.EquivalentTo(staging));
            var outer = Open(archiveFile);
            try {
                var innerDirectory = Sandbox.PathFor("extracted-inner");
                Directory.CreateDirectory(innerDirectory);
                await outer.ExtractAll(innerDirectory, overwrite: true, progress: null, token: CancellationToken.None);
                var innerArchive = Open(Path.Combine(innerDirectory, "inner.tar"));
                try {
                    var items = await innerArchive.EntryInfo(CancellationToken.None);
                    Assert.That(items.Items
                        .Where(item => item.Kind == TarEntryKind.File)
                        .Select(item => item.Name.Original),
                        Is.EquivalentTo(new[] { "folder/second.txt" }));
                }
                finally {
                    innerArchive.Release();
                }
            }
            finally {
                outer.Release();
            }
        }
        finally {
            path.Release();
            RemoveNewStagingDirectories(staging);
        }
    }

    [Test]
    public async Task Update_ReplacesTheParentEntryOfTheNestedArchiveThatIsUpdated() {
        var (archiveFile, inner) = await CreateArchiveInsideArchive();
        var path = OpenNested(archiveFile, inner);
        var staging = StagingDirectories();
        try {
            await path.Update(progress: null, token: CancellationToken.None, info: new TarArchiveUpdate {
                CreateEntriesFromFiles = new Dictionary<string, string> {
                    { "third.txt", Sandbox.WriteFile("third.txt", "third") }
                }
            });
            Assert.That(StagingDirectories(), Is.EquivalentTo(staging));
            var items = await path.EntryInfo(CancellationToken.None);
            Assert.That(items.Items
                .Where(item => item.Kind == TarEntryKind.File)
                .Select(item => item.Name.Original),
                Is.EquivalentTo(new[] { "first.txt", "folder/second.txt", "third.txt" }));
        }
        finally {
            path.Release();
            RemoveNewStagingDirectories(staging);
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
