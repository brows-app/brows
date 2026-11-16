using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
internal sealed class ZipIDTest {
    private ZipTestSandbox Sandbox;

    private static void AddFile(ZipArchive archive, string name, string contents) {
        var entry = archive.CreateEntry(name);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(contents);
        stream.Write(bytes);
    }

    [SetUp]
    public void SetUp() {
        Sandbox = new();
    }

    [TearDown]
    public void TearDown() {
        Sandbox.Dispose();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Parse_TreatsZipSuffixedDirectoriesAsArchivePaths(bool explicitDirectory) {
        var archivePath = Sandbox.PathFor("directories.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create)) {
            if (explicitDirectory) {
                archive.CreateEntry("folder.zip/");
            }
            AddFile(archive, "folder.zip/file.txt", "directory contents");
        }
        var id = await ZipID.Parse(
            ZipTestSandbox.ProviderID(archivePath, "folder.zip"),
            new ZipProviderFactory(),
            CancellationToken.None);
        Assert.That(id, Is.Not.Null);

        try {
            var entries = await id.EntryInfo(CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(id.ArchivePath.Nest.Count, Is.Zero);
                Assert.That(id.RelativePath.Normalized, Is.EqualTo("folder.zip"));
                Assert.That(entries.Select(entry => entry.Name.Original), Is.EqualTo(new[] {
                    "folder.zip/file.txt"
                }));
                Assert.That(id.Parent, Is.EqualTo($"zip:1|f={archivePath}|d="));
            }
        }
        finally {
            id.Release();
        }
    }

    [Test]
    public async Task Parse_TreatsZipSuffixedFilesAsNestedArchives() {
        var innerStream = new MemoryStream();
        using (var inner = new ZipArchive(innerStream, ZipArchiveMode.Create, leaveOpen: true)) {
            AddFile(inner, "inside.txt", "nested contents");
        }
        var archivePath = Sandbox.PathFor("outer.zip");
        using (var outer = ZipFile.Open(archivePath, ZipArchiveMode.Create)) {
            var entry = outer.CreateEntry("inner.zip", CompressionLevel.NoCompression);
            using var output = entry.Open();
            var bytes = innerStream.ToArray();
            output.Write(bytes);
        }
        innerStream.Dispose();

        var id = await ZipID.Parse(
            ZipTestSandbox.ProviderID(archivePath, nestedMembers: new[] { "inner.zip" }),
            new ZipProviderFactory(),
            CancellationToken.None);
        Assert.That(id, Is.Not.Null);

        try {
            var entries = await id.EntryInfo(CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(id.ArchivePath.Nest.Count, Is.EqualTo(1));
                Assert.That(entries.Select(entry => entry.Name.Original), Is.EqualTo(new[] {
                    "inside.txt"
                }));
            }
        }
        finally {
            id.Release();
        }
    }

    [Test]
    public async Task Parse_OpensDirectoryWithProviderDelimiter() {
        var archivePath = Sandbox.PathFor("delimited-directory.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create)) {
            AddFile(archive, "a>b/inside.txt", "directory contents");
        }

        var providerID = $"zip:1|f={archivePath}|d=a%3Eb";
        var id = await ZipID.Parse(providerID, new ZipProviderFactory(), CancellationToken.None);
        Assert.That(id, Is.Not.Null);

        try {
            var entries = await id.EntryInfo(CancellationToken.None);
            Assert.That(entries.Select(entry => entry.Name.Original), Is.EqualTo(new[] {
                "a>b/inside.txt"
            }));
        }
        finally {
            id?.Release();
        }
    }

    [Test]
    public async Task Parse_TreatsDelimiterBearingNestedFileAsExplicitNesting() {
        using var innerStream = new MemoryStream();
        using (var inner = new ZipArchive(innerStream, ZipArchiveMode.Create, leaveOpen: true)) {
            AddFile(inner, "inside.txt", "nested contents");
            AddFile(inner, "a>b/inside.txt", "nested delimiter directory");
        }

        var archivePath = Sandbox.PathFor("delimited-nested.zip");
        using (var outer = ZipFile.Open(archivePath, ZipArchiveMode.Create)) {
            var entry = outer.CreateEntry("folder/a>b.zip", CompressionLevel.NoCompression);
            using var output = entry.Open();
            output.Write(innerStream.ToArray());
        }

        var providerID = $"zip:1|f={archivePath}|n=folder/a%3Eb.zip|d=";
        var id = await ZipID.Parse(providerID, new ZipProviderFactory(), CancellationToken.None);
        Assert.That(id, Is.Not.Null);

        try {
            var entries = await id.EntryInfo(CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(id.ArchivePath.Nest.Count, Is.EqualTo(1));
                Assert.That(entries.Select(entry => entry.Name.Original), Is.EqualTo(new[] {
                    "a>b",
                    "inside.txt"
                }));
                Assert.That(id.FullName, Is.EqualTo(providerID));
                Assert.That(id.Parent, Is.EqualTo($"zip:1|f={archivePath}|d=folder"));
            }
        }
        finally {
            id?.Release();
        }

        var parent = await ZipID.Parse(
            $"zip:1|f={archivePath}|d=folder",
            new ZipProviderFactory(),
            CancellationToken.None);
        try {
            Assert.That(parent, Is.Not.Null);
            Assert.That(parent.RelativePath.Normalized, Is.EqualTo("folder"));
            Assert.That(parent.ArchivePath.Nest.Count, Is.Zero);
        }
        finally {
            parent?.Release();
        }

        var child = await ZipID.Parse(
            $"zip:1|f={archivePath}|n=folder/a%3Eb.zip|d=a%3Eb",
            new ZipProviderFactory(),
            CancellationToken.None);
        try {
            Assert.That(child, Is.Not.Null);
            Assert.That(child.Parent, Is.EqualTo($"zip:1|f={archivePath}|n=folder/a%3Eb.zip|d="));
            var childParent = await ZipID.Parse(child.Parent, new ZipProviderFactory(), CancellationToken.None);
            try {
                Assert.That(childParent, Is.Not.Null);
                Assert.That(childParent.ArchivePath.Nest.Count, Is.EqualTo(1));
                Assert.That(childParent.RelativePath.Parts.Count, Is.Zero);
            }
            finally {
                childParent?.Release();
            }
        }
        finally {
            child?.Release();
        }
    }

    [Test]
    public async Task Parse_RejectsLegacyCompositeIDs() {
        var archivePath = Sandbox.PathFor("legacy-id.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create)) {
            AddFile(archive, "folder/inside.txt", "contents");
        }

        var id = await ZipID.Parse($"{archivePath}>folder", new ZipProviderFactory(), CancellationToken.None);

        Assert.That(id, Is.Null);
    }

    [Test]
    public async Task Parse_RejectsMalformedOrOtherProviderIDsWithoutPathFallback() {
        var archivePath = Sandbox.PathFor("prefixed-id.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create)) {
            AddFile(archive, "inside.txt", "contents");
        }

        var inputs = new[] {
            $"zip:2|f={archivePath}|d=",
            $"zip:1|f={archivePath}|d=bad%escape",
            $"7z:1|f={archivePath}|d="
        };
        foreach (var input in inputs) {
            var id = await ZipID.Parse(input, new ZipProviderFactory(), CancellationToken.None);
            Assert.That(id, Is.Null, input);
        }
    }

    [Test]
    public async Task Parse_RejectsDirectoryFieldsThatNameFiles() {
        var archivePath = Sandbox.PathFor("directory-is-file.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create)) {
            AddFile(archive, "ordinary.txt", "contents");
        }

        var id = await ZipID.Parse(
            ZipTestSandbox.ProviderID(archivePath, "ordinary.txt"),
            new ZipProviderFactory(),
            CancellationToken.None);

        Assert.That(id, Is.Null);
    }

    [Test]
    public async Task Parse_RejectsNestingFieldsThatNameDirectories() {
        var archivePath = Sandbox.PathFor("nesting-is-directory.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create)) {
            AddFile(archive, "folder/inside.txt", "contents");
        }

        var id = await ZipID.Parse(
            ZipTestSandbox.ProviderID(archivePath, nestedMembers: new[] { "folder" }),
            new ZipProviderFactory(),
            CancellationToken.None);

        Assert.That(id, Is.Null);
    }

    [Test]
    public async Task Parse_OuterArchiveRootParentIsItsContainingFilesystemDirectory() {
        var archivePath = Sandbox.PathFor("root-parent.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create)) {
            AddFile(archive, "inside.txt", "contents");
        }

        var id = await ZipID.Parse(archivePath, new ZipProviderFactory(), CancellationToken.None);
        Assert.That(id, Is.Not.Null);
        try {
            Assert.That(id.FullName, Is.EqualTo($"zip:1|f={archivePath}|d="));
            Assert.That(id.Parent, Is.EqualTo(Path.GetDirectoryName(archivePath)));
        }
        finally {
            id.Release();
        }
    }

    [Test]
    public async Task Parse_ParentOfNestedDelimiterDirectoryReparses() {
        var archivePath = Sandbox.PathFor("delimited-parent.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create)) {
            AddFile(archive, "a>b/sub/inside.txt", "contents");
        }

        var id = await ZipID.Parse(
            ZipTestSandbox.ProviderID(archivePath, "a>b/sub"),
            new ZipProviderFactory(),
            CancellationToken.None);
        Assert.That(id, Is.Not.Null);
        try {
            var parentText = id.Parent;
            Assert.That(parentText, Is.EqualTo($"zip:1|f={archivePath}|d=a%3Eb"));
            var parent = await ZipID.Parse(parentText, new ZipProviderFactory(), CancellationToken.None);
            try {
                Assert.That(parent, Is.Not.Null);
                Assert.That(parent.RelativePath.Normalized, Is.EqualTo("a>b"));
                Assert.That((await parent.EntryInfo(CancellationToken.None)).Select(item => item.Name.Original),
                    Is.EqualTo(new[] { "a>b" + Path.DirectorySeparatorChar + "sub" }));
            }
            finally {
                parent?.Release();
            }
        }
        finally {
            id.Release();
        }
    }
}
