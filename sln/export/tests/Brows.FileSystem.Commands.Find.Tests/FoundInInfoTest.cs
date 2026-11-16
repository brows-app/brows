using Brows.Support;
using System.IO;

namespace Brows;

[TestFixture]
public sealed class FoundInInfoTest {
    [TestCase(false)]
    [TestCase(true)]
    public void RelativeDirectory_DirectChildIsEmpty(bool trailingSeparator) {
        using var fixture = new TempDirectory();
        var file = fixture.CreateFile("direct.txt");
        var rootPath = fixture.Root.FullName;
        if (trailingSeparator) {
            rootPath += Path.DirectorySeparatorChar;
        }
        var root = new DirectoryInfo(rootPath);
        var found = new FoundInInfo(file, root);

        Assert.That(found.RelativeDirectory, Is.Empty);
    }

    [Test]
    public void RelativeDirectory_ReturnsNestedPath() {
        using var fixture = new TempDirectory();
        var file = fixture.CreateFile(Path.Combine("nested", "deep", "entry.txt"));
        var found = new FoundInInfo(file, fixture.Root);

        Assert.That(found.RelativeDirectory, Is.EqualTo(Path.Combine("nested", "deep")));
    }
}
