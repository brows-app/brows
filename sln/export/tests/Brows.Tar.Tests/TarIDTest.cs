using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

/// <summary>
/// Tests the identity of a place inside a tar archive. The tests use real
/// archives that are created and removed on the real file system.
/// </summary>
[TestFixture]
internal sealed class TarIDTest {
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

    private static string EscapeIDField(string value) {
        return value
            .Replace("%", "%25", StringComparison.Ordinal)
            .Replace("|", "%7C", StringComparison.Ordinal)
            .Replace("=", "%3D", StringComparison.Ordinal)
            .Replace(">", "%3E", StringComparison.Ordinal)
            .Replace(" ", "%20", StringComparison.Ordinal);
    }

    private static string ProviderID(string archivePath, string directory, params string[] nestedMembers) {
        var fields = new List<string> {
            "tar:1",
            "f=" + EscapeIDField(archivePath)
        };
        fields.AddRange(nestedMembers.Select(member => "n=" + EscapeIDField(member)));
        fields.Add("d=" + EscapeIDField(directory));
        return string.Join("|", fields);
    }

    [Test]
    public async Task Parse_ReturnsTheIdentityOfTheArchive() {
        var archivePath = await CreateArchive("test.tar", ("first.txt", "first"));
        var id = await TarID.Parse(archivePath, new TarProviderFactory(), CancellationToken.None);
        try {
            Assert.That(id, Is.Not.Null);
            Assert.That(id.ArchivePath.FullName, Is.EqualTo(ProviderID(archivePath, "")));
            Assert.That(id.RelativePath.Original, Is.EqualTo(""));
        }
        finally {
            id?.Release();
        }
    }

    [Test]
    public async Task Parse_ReturnsTheSameArchivePathForCaseVariantPathsToTheSameFile() {
        var archivePath = await CreateArchive("test.tar", ("first.txt", "first"));
        var caseVariant = Path.Combine(
            Path.GetDirectoryName(archivePath),
            Path.GetFileName(archivePath).ToUpperInvariant());
        if (File.Exists(caseVariant) == false) {
            Assert.Ignore("The test directory is case-sensitive.");
        }
        var factory = new TarProviderFactory();
        var first = await TarID.Parse(archivePath, factory, CancellationToken.None);
        var second = await TarID.Parse(caseVariant, factory, CancellationToken.None);
        try {
            Assert.That(second.ArchivePath, Is.SameAs(first.ArchivePath));
        }
        finally {
            second?.Release();
            first?.Release();
        }
    }

    [Test]
    public async Task Parse_ReturnsTheIdentityOfAPlaceInsideTheArchive() {
        var archivePath = await CreateArchive("test.tar", ("folder/first.txt", "first"));
        var id = await TarID.Parse(
            ProviderID(archivePath, "folder"),
            new TarProviderFactory(),
            CancellationToken.None);
        try {
            Assert.That(id, Is.Not.Null);
            Assert.That(id.RelativePath.Original, Is.EqualTo("folder"));
        }
        finally {
            id?.Release();
        }
    }

    [TestCase("assets.tar")]
    [TestCase("assets.tar/subfolder")]
    public async Task Parse_ReturnsTheIdentityOfDirectoriesWithTarSuffix(string relativePath) {
        var archivePath = await CreateArchive("test.tar", ("assets.tar/subfolder/first.txt", "first"));
        var id = await TarID.Parse(
            ProviderID(archivePath, relativePath),
            new TarProviderFactory(),
            CancellationToken.None);
        try {
            Assert.That(id, Is.Not.Null);
            var normalizedPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            Assert.That(id.RelativePath.Normalized, Is.EqualTo(normalizedPath));
        }
        finally {
            id?.Release();
        }
    }

    [Test]
    public async Task Parse_IsNullForAFileInsideAnArchive() {
        var archivePath = await CreateArchive("test.tar", ("first.txt", "first"));
        var id = await TarID.Parse(
            ProviderID(archivePath, "first.txt"),
            new TarProviderFactory(),
            CancellationToken.None);
        try {
            Assert.That(id, Is.Null);
        }
        finally {
            id?.Release();
        }
    }

    [Test]
    public async Task Parse_IsNullForANonexistentDirectoryInsideAnArchive() {
        var archivePath = await CreateArchive("test.tar", ("first.txt", "first"));
        var id = await TarID.Parse(
            ProviderID(archivePath, "missing"),
            new TarProviderFactory(),
            CancellationToken.None);
        try {
            Assert.That(id, Is.Null);
        }
        finally {
            id?.Release();
        }
    }

    [Test]
    public async Task Parse_IsNullForAFileThatIsNotAnArchive() {
        var file = Sandbox.WriteFile("first.txt", "first");
        var id = await TarID.Parse(file, new TarProviderFactory(), CancellationToken.None);
        Assert.That(id, Is.Null);
    }

    [Test]
    public async Task Parse_IsNullForAFileThatDoesNotExist() {
        var id = await TarID.Parse(
            Sandbox.PathFor("missing.tar"),
            new TarProviderFactory(),
            CancellationToken.None);
        Assert.That(id, Is.Null);
    }

    [Test]
    public async Task Parse_IsNullForAnEmptyId() {
        var id = await TarID.Parse("", new TarProviderFactory(), CancellationToken.None);
        Assert.That(id, Is.Null);
    }

    [Test]
    public async Task Parse_ReturnsTheIdentityOfAnArchiveThatIsInsideAnArchive() {
        var innerPath = await CreateArchive("inner.tar", ("inside.txt", "nested"));
        var archivePath = Sandbox.PathFor("test.tar");
        await TarArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "inner.tar", innerPath }
        }, null, CancellationToken.None);
        var id = await TarID.Parse(
            ProviderID(archivePath, "", "inner.tar"),
            new TarProviderFactory(),
            CancellationToken.None);
        try {
            var items = await id.EntryInfo(CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(id, Is.Not.Null);
                Assert.That(id.ArchivePath.Nest.Count, Is.EqualTo(1));
                Assert.That(id.RelativePath.Original, Is.EqualTo(""));
                Assert.That(items.Select(item => item.Name.Original), Is.EquivalentTo(new[] { "inside.txt" }));
            }
        }
        finally {
            id?.Release();
        }
    }

    [Test]
    public async Task Parse_ReturnsTheIdentityOfAPlaceInsideAnArchiveThatIsInsideAnArchive() {
        var innerPath = await CreateArchive("inner.tar", ("deep/inside.txt", "nested"));
        var archivePath = Sandbox.PathFor("test.tar");
        await TarArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "inner.tar", innerPath }
        }, null, CancellationToken.None);
        var id = await TarID.Parse(
            ProviderID(archivePath, "deep", "inner.tar"),
            new TarProviderFactory(),
            CancellationToken.None);
        try {
            var items = await id.EntryInfo(CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(id, Is.Not.Null);
                Assert.That(id.ArchivePath.Nest.Count, Is.EqualTo(1));
                Assert.That(id.RelativePath.Normalized, Is.EqualTo("deep"));
                Assert.That(items.Select(item => item.Name.Original), Is.EquivalentTo(new[] { "deep/inside.txt" }));
            }
        }
        finally {
            id?.Release();
        }
    }

    [Test]
    public async Task Parse_IsNullForAnArchiveThatIsNotInsideAnArchive() {
        Sandbox.WriteFile("inner.tar", "not an archive");
        var archivePath = await CreateArchive("test.tar", ("first.txt", "first"));
        var id = await TarID.Parse(
            ProviderID(archivePath, "", "inner.tar"),
            new TarProviderFactory(),
            CancellationToken.None);
        Assert.That(id, Is.Null);
    }

    [Test]
    public async Task Parent_IsThePlaceInsideTheOuterArchiveForTheRootOfANestedArchive() {
        var innerPath = await CreateArchive("inner.tar", ("inside.txt", "nested"));
        var archivePath = Sandbox.PathFor("test.tar");
        await TarArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "a>b/inner.tar", innerPath }
        }, null, CancellationToken.None);
        var id = await TarID.Parse(
            ProviderID(archivePath, "", "a>b/inner.tar"),
            new TarProviderFactory(),
            CancellationToken.None);
        try {
            var parentID = id.Parent;
            var parent = await TarID.Parse(parentID, new TarProviderFactory(), CancellationToken.None);
            try {
                var parentEntries = await parent.EntryInfo(CancellationToken.None);
                using (Assert.EnterMultipleScope()) {
                    Assert.That(parentID, Is.EqualTo(ProviderID(archivePath, "a>b")));
                    Assert.That(parent, Is.Not.Null);
                    Assert.That(parent?.ArchivePath.Nest.Count, Is.Zero);
                    Assert.That(parent?.RelativePath.Normalized, Is.EqualTo("a>b"));
                    Assert.That(parentEntries.Select(item => item.Name.Original), Is.EquivalentTo(new[] {
                        "a>b/inner.tar"
                    }));
                }
            }
            finally {
                parent?.Release();
            }
        }
        finally {
            id.Release();
        }
    }

    [Test]
    public async Task EntryInfo_ListsTheItemsOfTheRootOfTheArchive() {
        var archivePath = await CreateArchive("test.tar", ("first.txt", "first"), ("folder/second.txt", "second"));
        var id = await TarID.Parse(archivePath, new TarProviderFactory(), CancellationToken.None);
        try {
            var items = await id.EntryInfo(CancellationToken.None);
            Assert.That(items.Select(item => item.Name.Original),
                Is.EquivalentTo(new[] { "first.txt", "folder" }));
        }
        finally {
            id.Release();
        }
    }

    [Test]
    public async Task EntryInfo_ListsTheItemsOfAFolderOfTheArchive() {
        var archivePath = await CreateArchive("test.tar", ("first.txt", "first"), ("folder/second.txt", "second"));
        var id = await TarID.Parse(
            ProviderID(archivePath, "folder"),
            new TarProviderFactory(),
            CancellationToken.None);
        try {
            var items = await id.EntryInfo(CancellationToken.None);
            Assert.That(items.Select(item => item.Name.Original),
                Is.EquivalentTo(new[] { "folder/second.txt" }));
        }
        finally {
            id.Release();
        }
    }

    [Test]
    public async Task Parent_IsTheFolderThatHoldsTheArchiveForTheRootOfTheArchive() {
        var archivePath = await CreateArchive("test.tar", ("first.txt", "first"));
        var id = await TarID.Parse(archivePath, new TarProviderFactory(), CancellationToken.None);
        try {
            Assert.That(id.Parent, Is.EqualTo(Path.GetDirectoryName(archivePath)));
        }
        finally {
            id.Release();
        }
    }

    [Test]
    public async Task Parent_IsThePlaceOfTheDelimiterBearingFolderForASubdirectory() {
        var source = Sandbox.WriteFile("parent-source.txt", "first");
        var archivePath = await CreateArchive("test.tar", new Dictionary<string, string> {
            { "a>b/sub/first.txt", source }
        });
        var id = await TarID.Parse(
            ProviderID(archivePath, "a>b/sub"),
            new TarProviderFactory(),
            CancellationToken.None);
        try {
            var parentID = id.Parent;
            var parent = await TarID.Parse(parentID, new TarProviderFactory(), CancellationToken.None);
            try {
                var entries = await parent.EntryInfo(CancellationToken.None);
                using (Assert.EnterMultipleScope()) {
                    Assert.That(parentID, Is.EqualTo(ProviderID(archivePath, "a>b")));
                    Assert.That(parent, Is.Not.Null);
                    Assert.That(parent?.RelativePath.Normalized, Is.EqualTo("a>b"));
                    Assert.That(entries.Select(item => item.Name.Original), Is.EquivalentTo(new[] {
                        "a>b" + Path.DirectorySeparatorChar + "sub"
                    }));
                }
            }
            finally {
                parent?.Release();
            }
        }
        finally {
            id?.Release();
        }
    }

    [Test]
    public async Task Parse_TreatsDelimiterBearingDirectoryAsOneDirectory() {
        var insidePath = Sandbox.WriteFile("directory-inside.txt", "contents");
        var archivePath = await CreateArchive("delimiter-directory.tar", new Dictionary<string, string> {
            { "a>b/inside.txt", insidePath }
        });
        var providerID = ProviderID(archivePath, "a>b");
        var id = await TarID.Parse(providerID, new TarProviderFactory(), CancellationToken.None);

        try {
            Assert.That(id, Is.Not.Null);
            var entries = await id.EntryInfo(CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(id.RelativePath.Normalized, Is.EqualTo("a>b"));
                Assert.That(entries.Select(entry => entry.Name.Original), Is.EquivalentTo(new[] {
                    "a>b/inside.txt"
                }));
                Assert.That(id.FullName, Is.EqualTo(providerID));
            }
        }
        finally {
            id?.Release();
        }
    }

    [Test]
    public async Task Parse_TreatsDelimiterBearingArchiveMemberAsOneNestedArchive() {
        var innerPath = await CreateArchive("inner-source.tar", ("inside.txt", "nested contents"));
        var archivePath = await CreateArchive("outer.tar", new Dictionary<string, string> {
            { "a>b.tar", innerPath },
            { "a>b-directory/inside.txt", Sandbox.WriteFile("outer-inside.txt", "outer contents") }
        });
        var providerID = ProviderID(archivePath, "", "a>b.tar");
        var id = await TarID.Parse(providerID, new TarProviderFactory(), CancellationToken.None);

        try {
            Assert.That(id, Is.Not.Null);
            var entries = await id.EntryInfo(CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(id.ArchivePath.Nest.Count, Is.EqualTo(1));
                Assert.That(id.RelativePath.Parts, Is.Empty);
                Assert.That(entries.Select(entry => entry.Name.Original), Is.EquivalentTo(new[] {
                    "inside.txt"
                }));
                Assert.That(id.FullName, Is.EqualTo(providerID));
            }
        }
        finally {
            id?.Release();
        }
    }

    [Test]
    public async Task Parse_UsesExplicitFieldsToDistinguishDirectoriesAndFiles() {
        var directorySource = Sandbox.WriteFile("directory-source.txt", "directory child");
        var archivePath = await CreateArchive("explicit-fields.tar", new Dictionary<string, string> {
            { "assets.tar/inside.txt", directorySource },
            { "ordinary.txt", Sandbox.WriteFile("ordinary-source.txt", "file") }
        });
        var factory = new TarProviderFactory();
        var directory = await TarID.Parse(
            ProviderID(archivePath, "assets.tar"),
            factory,
            CancellationToken.None);
        var nestedDirectory = await TarID.Parse(
            ProviderID(archivePath, "", "assets.tar"),
            factory,
            CancellationToken.None);
        var fileDirectory = await TarID.Parse(
            ProviderID(archivePath, "ordinary.txt"),
            factory,
            CancellationToken.None);

        try {
            using (Assert.EnterMultipleScope()) {
                Assert.That(directory, Is.Not.Null);
                Assert.That(directory?.RelativePath.Normalized, Is.EqualTo("assets.tar"));
                Assert.That(nestedDirectory, Is.Null);
                Assert.That(fileDirectory, Is.Null);
            }
        }
        finally {
            directory?.Release();
            nestedDirectory?.Release();
            fileDirectory?.Release();
        }
    }

    [Test]
    public async Task Parse_IsNullForMalformedPrefixedID() {
        var archivePath = await CreateArchive("malformed-prefix.tar", ("keep.txt", "contents"));
        var id = await TarID.Parse(
            $"tar:1|f={archivePath}|d=%Q0",
            new TarProviderFactory(),
            CancellationToken.None);

        Assert.That(id, Is.Null);
    }

    [Test]
    public async Task Parse_IsNullForAnIDBelongingToAnotherArchiveProvider() {
        var archivePath = await CreateArchive("wrong-provider.tar", ("keep.txt", "contents"));
        var id = await TarID.Parse(
            $"zip:1|f={archivePath}|d=",
            new TarProviderFactory(),
            CancellationToken.None);

        Assert.That(id, Is.Null);
    }

    [Test]
    public async Task Parse_IsNullForATarIDWithANonTarOuterFile() {
        var archivePath = Sandbox.WriteFile("wrong-extension.zip", "not a tar archive");
        var id = await TarID.Parse(
            ProviderID(archivePath, ""),
            new TarProviderFactory(),
            CancellationToken.None);

        Assert.That(id, Is.Null);
    }

    [Test]
    public async Task Parse_IsNullForARelativeArchivePathInAStructuredID() {
        var id = await TarID.Parse(
            "tar:1|f=relative.tar|d=",
            new TarProviderFactory(),
            CancellationToken.None);

        Assert.That(id, Is.Null);
    }
}
