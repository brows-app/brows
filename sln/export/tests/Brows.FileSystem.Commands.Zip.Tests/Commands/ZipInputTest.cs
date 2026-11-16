using System.IO;
using System.Linq;

namespace Brows.Commands;

[TestFixture]
internal sealed class ZipInputTest {
    private ZipCommandTestSandbox Sandbox;

    [SetUp]
    public void SetUp() {
        Sandbox = new();
    }

    [TearDown]
    public void TearDown() {
        Sandbox.Dispose();
    }

    [TestCase("child/deeper/file.txt", false)]
    [TestCase("child/file.txt", false)]
    [TestCase("child/deeper/file.txt", true)]
    public void Entries_PreservesPathsBelowASingleSelectedDirectory(string relativePath, bool commonSubtree) {
        var directory = Sandbox.CreateDirectory("selected");
        var file = Sandbox.WriteFile("selected/" + relativePath, "contents");
        if (commonSubtree) {
            Sandbox.WriteFile(Path.Combine("selected", Path.GetDirectoryName(relativePath)!, "peer.txt"), "peer");
        }
        var input = new ZipInput(true, new FileSystemInfo[] { new DirectoryInfo(directory) });

        var entry = input.Entries(null, CancellationToken.None).Single(item => item.FilePath == file);

        Assert.That(entry.EntryName, Is.EqualTo(relativePath));
    }

    [Test]
    public void Entries_ExistingMemberNamesDoNotDependOnOtherDescendantFiles() {
        var directory = Sandbox.CreateDirectory("selected");
        var file = Sandbox.WriteFile("selected/subtree/deeper/first.txt", "first");
        var input = new ZipInput(true, new FileSystemInfo[] { new DirectoryInfo(directory) });
        var before = input.Entries(null, CancellationToken.None).Single().EntryName;
        Sandbox.WriteFile("selected/other/second.txt", "second");

        var after = input.Entries(null, CancellationToken.None).Single(entry => entry.FilePath == file).EntryName;

        using (Assert.EnterMultipleScope()) {
            Assert.That(before, Is.EqualTo("subtree/deeper/first.txt"));
            Assert.That(after, Is.EqualTo(before));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Entries_MultipleSelectedDirectoriesPreserveTheirNamesAndSubtrees(bool secondContainsFiles) {
        var first = Sandbox.CreateDirectory("first");
        var second = Sandbox.CreateDirectory("second");
        Sandbox.WriteFile("first/shared/deeper/file.txt", "first contents");
        Sandbox.CreateDirectory("first/empty/deeper");
        if (secondContainsFiles) {
            Sandbox.WriteFile("second/other/file.txt", "second contents");
        }
        var input = new ZipInput(true, new FileSystemInfo[] {
            new DirectoryInfo(first),
            new DirectoryInfo(second)
        });

        var entries = input.Entries(null, CancellationToken.None).Select(entry => entry.EntryName).ToList();

        Assert.That(entries, Is.EquivalentTo(new[] {
            "first/shared/deeper/file.txt",
            "first/empty/deeper",
            secondContainsFiles ? "second/other/file.txt" : "second"
        }));
    }

    [Test]
    public void Entries_MixedSelectionsUseTheirCommonParent() {
        var directory = Sandbox.CreateDirectory("selected");
        Sandbox.WriteFile("selected/child/deeper/file.txt", "contents");
        var loose = Sandbox.WriteFile("loose.txt", "loose contents");
        var input = new ZipInput(true, new FileSystemInfo[] {
            new DirectoryInfo(directory),
            new FileInfo(loose)
        });

        var entries = input.Entries(null, CancellationToken.None).Select(entry => entry.EntryName);

        Assert.That(entries, Is.EquivalentTo(new[] { "selected/child/deeper/file.txt", "loose.txt" }));
    }

    [Test]
    public void Entries_SingleSelectedFileUsesItsFileName() {
        var file = Sandbox.WriteFile("parent/child/file.txt", "contents");
        var input = new ZipInput(true, new FileSystemInfo[] { new FileInfo(file) });

        var entry = input.Entries(null, CancellationToken.None).Single();

        Assert.That(entry.EntryName, Is.EqualTo("file.txt"));
    }

    [Test]
    public void Entries_IncludesASelectedEmptyDirectory() {
        var directory = Sandbox.CreateDirectory("empty");
        var input = new ZipInput(true, new FileSystemInfo[] {
            new DirectoryInfo(directory)
        });

        var entry = input.Entries(null, CancellationToken.None).Single();

        using (Assert.EnterMultipleScope()) {
            Assert.That(entry.EntryName, Is.EqualTo("empty"));
            Assert.That(entry.FilePath, Is.EqualTo(directory));
            Assert.That(entry.IsDirectory, Is.True);
        }
    }

    [Test]
    public void Entries_IncludesEmptyDescendantsAlongsideFiles() {
        var directory = Sandbox.CreateDirectory("source");
        Sandbox.CreateDirectory("source/empty-child");
        Sandbox.WriteFile("source/file.txt", "contents");
        var input = new ZipInput(true, new FileSystemInfo[] {
            new DirectoryInfo(directory)
        });

        var entries = input.Entries(null, CancellationToken.None).ToList();

        using (Assert.EnterMultipleScope()) {
            Assert.That(entries.Count, Is.EqualTo(2));
            Assert.That(entries.Any(entry => entry.IsDirectory &&
                entry.EntryName.EndsWith("empty-child", StringComparison.Ordinal)), Is.True);
            Assert.That(entries.Any(entry => entry.IsDirectory is false &&
                Path.GetFileName(entry.FilePath) == "file.txt"), Is.True);
        }
    }

    [Test]
    public void Entries_PreservesNestedEmptyDirectoryHierarchyWithoutFiles() {
        var directory = Sandbox.CreateDirectory("source");
        Sandbox.CreateDirectory("source/child/deeper/empty");
        Sandbox.CreateDirectory("source/branch/empty");
        var input = new ZipInput(true, new FileSystemInfo[] {
            new DirectoryInfo(directory)
        });

        var entries = input.Entries(null, CancellationToken.None)
            .Where(entry => entry.IsDirectory)
            .Select(entry => entry.EntryName)
            .ToList();

        Assert.That(entries, Is.EquivalentTo(new[] {
            "child/deeper/empty",
            "branch/empty"
        }));
    }
}
