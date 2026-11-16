using Brows.Composition;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brows;

internal sealed partial class ZipProviderTest {
    [Test]
    public async Task CopyProvidedIO_PreservesWhitespaceInARealZipSourceName() {
        using var sandbox = new ZipTestSandbox();
        var sourceArchivePath = sandbox.PathFor("source.zip");
        using (var sourceArchive = ZipFile.Open(sourceArchivePath, ZipArchiveMode.Create)) {
            AddFile(sourceArchive, " report.txt ", "source whitespace member");
        }
        var targetArchivePath = sandbox.PathFor("target.zip");
        using (var targetArchive = ZipFile.Open(targetArchivePath, ZipArchiveMode.Create)) {
            AddFile(targetArchive, "report.txt", "existing root member");
        }

        var sourceFactory = new ZipProviderFactory();
        var sourceID = await ZipID.Parse(sourceArchivePath, sourceFactory, CancellationToken.None);
        var targetFactory = new ZipProviderFactory();
        var targetID = await ZipID.Parse(targetArchivePath, targetFactory, CancellationToken.None);
        var sourceProvider = new ZipProvider(sourceID, sourceFactory);
        var targetProvider = new ZipProvider(targetID, targetFactory);
        try {
            var info = (await sourceID.ArchivePath.EntryInfo(CancellationToken.None)).Items
                .Single(item => item.Name.Original == " report.txt ");
            var source = new ZipStreamSource(new ZipEntry(sourceProvider, info));
            var copied = await CopyService().Work(new[] {
                new ProvidedIO {
                    StreamSets = new[] { new ZipStreamSet(new[] { source }) }
                }
            }, targetProvider, null, CancellationToken.None);

            using var result = ZipFile.OpenRead(targetArchivePath);
            using var existing = new StreamReader(result.GetEntry("report.txt")!.Open(), Encoding.UTF8);
            using var imported = new StreamReader(result.GetEntry(" report.txt ")!.Open(), Encoding.UTF8);
            using (Assert.EnterMultipleScope()) {
                Assert.That(copied, Is.True);
                Assert.That(existing.ReadToEnd(), Is.EqualTo("existing root member"));
                Assert.That(imported.ReadToEnd(), Is.EqualTo("source whitespace member"));
            }
        }
        finally {
            sourceProvider.Dispose();
            sourceID.Release();
            targetProvider.Dispose();
            targetID.Release();
        }
    }
}
