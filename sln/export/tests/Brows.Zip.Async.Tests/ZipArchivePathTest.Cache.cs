using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Brows;

internal sealed partial class ZipArchivePathTest {
    private static void DisableCacheWatcher(ZipArchivePath path) {
        var field = typeof(ZipArchivePath).GetField("FileEvent", BindingFlags.Instance | BindingFlags.NonPublic);
        ((IDisposable)field!.GetValue(path)!).Dispose();
    }

    private static Task DispatchCacheEvent(
        ZipArchivePath path,
        FileSystemEventArgs change,
        CancellationToken token) {
        var method = typeof(ZipArchivePath).GetMethod("FileEventTask", BindingFlags.Instance | BindingFlags.NonPublic);
        return (Task)method!.Invoke(path, new object[] { change, token })!;
    }

    private static byte[] CreateCachedOuterArchive(string memberName) {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true)) {
            archive.CreateEntry(memberName);
            foreach (var nestedName in new[] { "first.zip", "second.zip" }) {
                var nested = archive.CreateEntry(nestedName);
                using var output = nested.Open();
                output.Write(CreateArchiveBytes(new[] { (memberName, memberName) }));
            }
        }
        return stream.ToArray();
    }

    private static ZipArchivePath[] OpenCachedPaths(string archiveFileName, string unrelatedFileName) {
        var paths = new[] {
            OpenUpdateArchive(archiveFileName),
            OpenUpdateArchive(archiveFileName, "first.zip"),
            OpenUpdateArchive(archiveFileName, "second.zip"),
            OpenUpdateArchive(unrelatedFileName)
        };
        foreach (var path in paths) {
            DisableCacheWatcher(path);
        }
        return paths;
    }

    [Test]
    public void Get_UsesUnambiguousNestingIdentityForTheCacheKey() {
        var file = CreateArchive("ambiguous-nesting.zip", ("keep.txt", "keep"));
        var first = OpenUpdateArchive(file, "folder>inner.zip");
        var second = OpenUpdateArchive(file, "folder", "inner.zip");
        var repeatedFirst = OpenUpdateArchive(file, "folder>inner.zip");
        ZipArchivePath thirdSecond = null;
        try {
            using (Assert.EnterMultipleScope()) {
                Assert.That(first.FullName, Does.Contain("|n=folder%3Einner.zip|d="));
                Assert.That(second.FullName, Does.Contain("|n=folder|n=inner.zip|d="));
                Assert.That(first, Is.Not.SameAs(second));
                Assert.That(repeatedFirst, Is.SameAs(first));
            }

            first.Release();
            first = null;
            repeatedFirst.Release();
            repeatedFirst = null;
            thirdSecond = OpenUpdateArchive(file, "folder", "inner.zip");
            using (Assert.EnterMultipleScope()) {
                Assert.That(thirdSecond, Is.SameAs(second));
                Assert.That(thirdSecond.FullName, Does.Contain("|n=folder|n=inner.zip|d="));
            }
        }
        finally {
            first?.Release();
            second?.Release();
            repeatedFirst?.Release();
            thirdSecond?.Release();
        }
    }

    [TestCase(WatcherChangeTypes.Changed, false)]
    [TestCase(WatcherChangeTypes.Created, false)]
    [TestCase(WatcherChangeTypes.Renamed, false)]
    [TestCase(WatcherChangeTypes.Renamed, true)]
    public async Task FileEvent_PreservedTimestampReloadsMetadataBeforeNotifying(
        WatcherChangeTypes changeType,
        bool matchesOldName) {
        var file = CreateArchive("cache-event.zip", ("old.txt", "old"));
        var timestamp = File.GetLastWriteTimeUtc(file);
        var path = OpenUpdateArchive(file);
        DisableCacheWatcher(path);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try {
            var before = await path.EntryInfo(timeout.Token);
            ZipEntryInfoCollection notified = null;
            path.FileChanged.Add(async token => {
                notified = await path.EntryInfo(token);
            });
            File.WriteAllBytes(file, CreateArchiveBytes(new[] { ("new.txt", "replacement") }));
            File.SetLastWriteTimeUtc(file, timestamp);
            var name = Path.GetFileName(file);
            FileSystemEventArgs change = changeType == WatcherChangeTypes.Renamed
                ? new RenamedEventArgs(
                    changeType,
                    Path.GetDirectoryName(file)!,
                    matchesOldName ? "renamed.zip" : name,
                    matchesOldName ? name : "incoming.zip")
                : new FileSystemEventArgs(changeType, Path.GetDirectoryName(file)!, name);

            await DispatchCacheEvent(path, change, timeout.Token);

            using (Assert.EnterMultipleScope()) {
                Assert.That(File.GetLastWriteTimeUtc(file), Is.EqualTo(timestamp));
                Assert.That(notified, Is.Not.Null);
                Assert.That(notified?.Items.Select(info => info.Name.Original), Is.EqualTo(new[] { "new.txt" }));
                Assert.That(await path.EntryInfo(timeout.Token), Is.Not.SameAs(before));
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task FileEvent_InvalidatesOuterAndSiblingNestedCachesWithoutInvalidatingOtherArchives() {
        var file = UpdateSandbox.PathFor("shared-cache.zip");
        File.WriteAllBytes(file, CreateCachedOuterArchive("old.txt"));
        var timestamp = File.GetLastWriteTimeUtc(file);
        var unrelated = CreateArchive("unrelated-cache.zip", ("keep.txt", "keep"));
        var paths = OpenCachedPaths(file, unrelated);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try {
            var before = await Task.WhenAll(paths.Select(path => path.EntryInfo(timeout.Token)));
            foreach (var path in paths.Take(3)) {
                path.FileChanged.Add(async token => {
                    await path.EntryInfo(token);
                });
            }
            File.WriteAllBytes(file, CreateCachedOuterArchive("new.txt"));
            File.SetLastWriteTimeUtc(file, timestamp);

            foreach (var path in paths.Take(3)) {
                await DispatchCacheEvent(path, new FileSystemEventArgs(
                    WatcherChangeTypes.Changed,
                    Path.GetDirectoryName(file)!,
                    Path.GetFileName(file)), timeout.Token);
            }

            foreach (var path in paths.Take(3)) {
                Assert.That(path.EntryInfo(), Is.Not.Null,
                    "A sibling notification must not discard metadata already reloaded by this path's listener.");
            }

            var after = await Task.WhenAll(paths.Select(path => path.EntryInfo(timeout.Token)));
            using (Assert.EnterMultipleScope()) {
                for (var index = 0; index < 3; index++) {
                    Assert.That(after[index], Is.Not.SameAs(before[index]));
                    Assert.That(after[index].Items.Select(info => info.Name.Original), Does.Contain("new.txt"));
                    Assert.That(after[index].Items.Select(info => info.Name.Original), Does.Not.Contain("old.txt"));
                }
                Assert.That(after[3], Is.SameAs(before[3]));
            }
        }
        finally {
            foreach (var path in paths) {
                path.Release();
            }
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Update_CommitInvalidatesAllRelatedCachesWithoutWatcherEvents(bool nestedUpdate) {
        var file = UpdateSandbox.PathFor("committed-cache.zip");
        File.WriteAllBytes(file, CreateCachedOuterArchive("old.txt"));
        var unrelated = CreateArchive("unrelated-cache.zip", ("keep.txt", "keep"));
        var source = UpdateSandbox.WriteFile("new-source.txt", "new contents");
        var paths = OpenCachedPaths(file, unrelated);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try {
            var before = await Task.WhenAll(paths.Select(path => path.EntryInfo(timeout.Token)));
            var target = paths[nestedUpdate ? 1 : 0];
            await target.Update(new ZipArchiveUpdate {
                DeleteEntries = new() { "old.txt" },
                CreateEntriesFromFiles = new() { ["new.txt"] = source }
            }, progress: null, timeout.Token);

            var after = await Task.WhenAll(paths.Select(path => path.EntryInfo(timeout.Token)));
            using (Assert.EnterMultipleScope()) {
                for (var index = 0; index < 3; index++) {
                    Assert.That(after[index], Is.Not.SameAs(before[index]));
                }
                var changed = after[nestedUpdate ? 1 : 0].Items.Select(info => info.Name.Original);
                Assert.That(changed, Does.Contain("new.txt"));
                Assert.That(changed, Does.Not.Contain("old.txt"));
                Assert.That(after[3], Is.SameAs(before[3]));
            }
        }
        finally {
            foreach (var path in paths) {
                path.Release();
            }
        }
    }

    [Test]
    public async Task FileEvent_DeletionClearsOuterAndNestedMetadataBeforeNotifying() {
        var file = UpdateSandbox.PathFor("deleted-cache.zip");
        File.WriteAllBytes(file, CreateCachedOuterArchive("old.txt"));
        var root = OpenUpdateArchive(file);
        var nested = OpenUpdateArchive(file, "first.zip");
        DisableCacheWatcher(root);
        DisableCacheWatcher(nested);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try {
            await root.EntryInfo(timeout.Token);
            await nested.EntryInfo(timeout.Token);
            var deleted = false;
            foreach (var path in new[] { root, nested }) {
                path.FileDeleted.Add(token => {
                    deleted = true;
                    Assert.That(path.EntryInfo(), Is.Null);
                    return Task.CompletedTask;
                });
            }
            File.Delete(file);

            foreach (var path in new[] { root, nested }) {
                await DispatchCacheEvent(path, new FileSystemEventArgs(
                    WatcherChangeTypes.Deleted,
                    Path.GetDirectoryName(file)!,
                    Path.GetFileName(file)), timeout.Token);
            }

            using (Assert.EnterMultipleScope()) {
                Assert.That(deleted, Is.True);
                Assert.That(root.EntryInfo(), Is.Null);
                Assert.That(nested.EntryInfo(), Is.Null);
            }
        }
        finally {
            root.Release();
            nested.Release();
        }
    }

    [Test]
    public async Task FileEvent_UnrelatedNameKeepsCachedMetadataAndDoesNotNotify() {
        var file = CreateArchive("unchanged-cache.zip", ("keep.txt", "keep"));
        var path = OpenUpdateArchive(file);
        DisableCacheWatcher(path);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try {
            var before = await path.EntryInfo(timeout.Token);
            var notified = false;
            path.FileChanged.Add(token => {
                notified = true;
                return Task.CompletedTask;
            });

            await DispatchCacheEvent(path, new FileSystemEventArgs(
                WatcherChangeTypes.Changed,
                Path.GetDirectoryName(file)!,
                "unrelated.txt"), timeout.Token);

            using (Assert.EnterMultipleScope()) {
                Assert.That(await path.EntryInfo(timeout.Token), Is.SameAs(before));
                Assert.That(notified, Is.False);
            }
        }
        finally {
            path.Release();
        }
    }

    [Test]
    public async Task Update_FailedCommitKeepsCachedMetadata() {
        var file = CreateArchive("failed-cache.zip", ("keep.txt", "keep"));
        var path = OpenUpdateArchive(file);
        DisableCacheWatcher(path);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try {
            var before = await path.EntryInfo(timeout.Token);
            using var blocker = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);

            await Assert.ThrowsAsync<IOException>(() => path.Update(new ZipArchiveUpdate {
                DeleteEntries = new() { "keep.txt" }
            }, progress: null, timeout.Token));

            Assert.That(await path.EntryInfo(timeout.Token), Is.SameAs(before));
        }
        finally {
            path.Release();
        }
    }
}
