using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Brows.IO.Compression;

[TestFixture]
internal sealed class ZipArchiveExtensionTest {
    [Test]
    public void Delete_RemovesEveryExactDuplicateAndPreservesOtherNames() {
        using var contents = new MemoryStream();
        using (var created = new ZipArchive(contents, ZipArchiveMode.Create, leaveOpen: true)) {
            created.CreateEntry("same.txt");
            created.CreateEntry("same.txt");
            created.CreateEntry("SAME.txt");
            created.CreateEntry("same.txt/child.txt");
        }
        contents.Position = 0;
        using var archive = new ZipArchive(contents, ZipArchiveMode.Update);

        archive.Delete("same.txt");

        Assert.That(archive.Entries.Select(entry => entry.FullName), Is.EqualTo(new[] {
            "SAME.txt", "same.txt/child.txt"
        }));
    }
}
