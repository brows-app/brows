using Brows.Providers;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Brows;

internal sealed partial class ZipProviderTest {
    [TestCase(false)]
    [TestCase(true)]
    public async Task Refresh_ReloadsMetadataWithoutWatcherEvents(bool internalUpdate) {
        var archiveFileName = Sandbox.PathFor("refresh.zip");
        using (var archive = ZipFile.Open(archiveFileName, ZipArchiveMode.Create)) {
            AddFile(archive, "old.txt", "old contents");
        }
        var factory = new ZipProviderFactory();
        var id = await ZipID.Parse(archiveFileName, factory, CancellationToken.None);
        var provider = new ZipProvider(id, factory);
        var watcher = typeof(ZipArchivePath).GetField("FileEvent", BindingFlags.Instance | BindingFlags.NonPublic);
        ((IDisposable)watcher!.GetValue(id.ArchivePath)!).Dispose();
        var config = typeof(EntryProvider).GetProperty("Config", BindingFlags.Instance | BindingFlags.NonPublic);
        config!.SetValue(provider, new ZipConfig());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try {
            var target = (IEntryProvider)provider;
            await target.Refresh(timeout.Token);
            Assert.That(target.Observation.Observed.Cast<ZipEntry>().Select(entry => entry.Info.Name.Original),
                Is.EqualTo(new[] { "old.txt" }));
            var timestamp = File.GetLastWriteTimeUtc(archiveFileName);
            if (internalUpdate) {
                var source = Sandbox.PathFor("new-source.txt");
                File.WriteAllText(source, "new contents");
                await id.ArchivePath.Update(new ZipArchiveUpdate {
                    DeleteEntries = new() { "old.txt" },
                    CreateEntriesFromFiles = new() { ["new.txt"] = source }
                }, progress: null, timeout.Token);
            }
            else {
                File.Delete(archiveFileName);
                using var archive = ZipFile.Open(archiveFileName, ZipArchiveMode.Create);
                AddFile(archive, "new.txt", "new contents");
            }
            File.SetLastWriteTimeUtc(archiveFileName, timestamp);

            await target.Refresh(timeout.Token);

            Assert.That(target.Observation.Observed.Cast<ZipEntry>().Select(entry => entry.Info.Name.Original),
                Is.EqualTo(new[] { "new.txt" }));
        }
        finally {
            provider.Dispose();
            id.Release();
        }
    }
}
