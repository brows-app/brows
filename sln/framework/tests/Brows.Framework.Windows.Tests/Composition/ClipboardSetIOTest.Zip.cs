using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brows.Composition;

internal sealed partial class ClipboardSetIOTest {
    [Test]
    public async Task Work_StagesRealZipDirectoryStreamsForTheClipboard() {
        var archivePath = Path.Combine(Path.GetTempPath(), $"BrowsClipboardZip-{Guid.NewGuid():N}.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create)) {
            archive.CreateEntry("folder/");
            archive.CreateEntry("folder/empty/");
            var file = archive.CreateEntry("folder/nested/file.txt");
            using var stream = file.Open();
            var contents = Encoding.UTF8.GetBytes("clipboard zip contents");
            stream.Write(contents);
        }

        var factory = new ZipProviderFactory();
        var id = await ZipID.Parse(archivePath, factory, CancellationToken.None);
        var provider = new ZipProvider(id, factory);
        try {
            var directoryPrefix = "folder" + Path.DirectorySeparatorChar;
            var infos = (await id.ArchivePath.EntryInfo(CancellationToken.None)).Items
                .Where(info =>
                    info.Name.Normalized == "folder" ||
                    info.Name.Normalized.StartsWith(directoryPrefix, StringComparison.Ordinal))
                .ToList();
            var sources = infos
                .Select(info => new ZipStreamSource(new ZipEntry(provider, info)))
                .ToList();
            var set = new ClipboardSetIO(Transport);
            var worked = await set.Work(new[] {
                new ProvidedIO {
                    StreamSets = new[] { new ZipStreamSet(sources) }
                }
            }, new TestClipboardSetIOData(), null, CancellationToken.None);

            using (Assert.EnterMultipleScope()) {
                Assert.That(worked, Is.True);
                Assert.That(Transport.Files.Count, Is.EqualTo(1));
                Assert.That(Path.GetFileName(Transport.Files[0]), Is.EqualTo("folder"));
            }
            PreparedPath = Transport.Files[0];
            Assert.That(Directory.Exists(Path.Combine(PreparedPath, "empty")), Is.True);
            var nestedFile = Path.Combine(PreparedPath, "nested", "file.txt");
            Assert.That(File.ReadAllText(nestedFile), Is.EqualTo("clipboard zip contents"));
        }
        finally {
            provider.Dispose();
            id.Release();
            File.Delete(archivePath);
        }
    }
}
