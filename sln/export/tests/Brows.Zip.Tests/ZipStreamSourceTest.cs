using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
internal sealed partial class ZipStreamSourceTest {
    private static void CreateArchive(string archivePath, string entryName) {
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        var contents = Encoding.UTF8.GetBytes("ZIP stream path test");
        stream.Write(contents);
    }

    private static async Task<(ZipProvider Provider, ZipEntryInfo Info)> LoadEntry(
        string archivePath,
        string entryName) {
        var factory = new ZipProviderFactory();
        var id = await ZipID.Parse(archivePath, factory, CancellationToken.None);
        if (id is null) {
            throw new InvalidOperationException("The test archive could not be opened.");
        }

        try {
            var entries = await id.ArchivePath.EntryInfo(CancellationToken.None);
            var info = entries.Items.Single(item => item.Name.Original == entryName);
            return (new ZipProvider(id, factory), info);
        }
        catch {
            id.Release();
            throw;
        }
    }

    [TestCase("../outside.txt")]
    [TestCase("folder/../../outside.txt")]
    [TestCase("C:outside.txt")]
    [TestCase("/outside.txt")]
    [TestCase("\\\\server\\share\\outside.txt")]
    [TestCase("CON.txt")]
    [TestCase("COM1.txt")]
    [TestCase("LPT\u00B9.txt")]
    [TestCase("folder/name:stream.txt")]
    [TestCase("name.")]
    public async Task RelativePath_RejectsUnsafeArchiveMemberPaths(string memberName) {
        using var sandbox = new ZipTestSandbox();
        var archivePath = sandbox.PathFor("unsafe.zip");
        CreateArchive(archivePath, memberName);
        var (provider, info) = await LoadEntry(archivePath, memberName);

        try {
            var entry = new ZipEntry(provider, info);
            var source = new ZipStreamSource(entry);
            Assert.That(() => source.RelativePath, Throws.TypeOf<InvalidDataException>());
        }
        finally {
            provider.Dispose();
            provider.Zip.Release();
        }
    }

    [Test]
    public async Task RelativePath_PreservesSafeNestedArchiveMemberPath() {
        using var sandbox = new ZipTestSandbox();
        var archivePath = sandbox.PathFor("safe.zip");
        CreateArchive(archivePath, "folder/file.txt");
        var (provider, info) = await LoadEntry(archivePath, "folder/file.txt");

        try {
            var source = new ZipStreamSource(new ZipEntry(provider, info));
            Assert.That(source.RelativePath, Is.EqualTo("folder" + Path.DirectorySeparatorChar + "file.txt"));
        }
        finally {
            provider.Dispose();
            provider.Zip.Release();
        }
    }
}
