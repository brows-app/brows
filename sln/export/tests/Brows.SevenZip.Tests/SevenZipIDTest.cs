using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

/// <summary>
/// Tests the identity of a place inside a 7-Zip archive. The tests use real
/// archives that are created and removed on the real file system.
/// </summary>
[TestFixture]
internal sealed class SevenZipIDTest {
    private SevenZipTestSandbox Sandbox;

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
        await SevenZipArchiveFile.Create(archivePath, files.ToDictionary(
            file => file.Path,
            file => Sandbox.WriteFile(file.Path, file.Contents)), null, null, CancellationToken.None);
        return archivePath;
    }

    private async Task<string> CreateArchive(string name, IReadOnlyDictionary<string, string> files) {
        var archivePath = Sandbox.PathFor(name);
        await SevenZipArchiveFile.Create(archivePath, files, null, null, CancellationToken.None);
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
            "7z:1",
            "f=" + EscapeIDField(archivePath)
        };
        fields.AddRange(nestedMembers.Select(member => "n=" + EscapeIDField(member)));
        fields.Add("d=" + EscapeIDField(directory));
        return string.Join("|", fields);
    }

    [Test]
    public async Task Parse_ReturnsTheIdentityOfTheArchive() {
        var archivePath = await CreateArchive("test.7z", ("first.txt", "first"));
        var id = await SevenZipID.Parse(archivePath, new SevenZipProviderFactory(), CancellationToken.None);
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
        var archivePath = await CreateArchive("test.7z", ("first.txt", "first"));
        var caseVariant = Path.Combine(
            Path.GetDirectoryName(archivePath),
            Path.GetFileName(archivePath).ToUpperInvariant());
        if (File.Exists(caseVariant) == false) {
            Assert.Ignore("The test directory is case-sensitive.");
        }
        var factory = new SevenZipProviderFactory();
        var first = await SevenZipID.Parse(archivePath, factory, CancellationToken.None);
        var second = await SevenZipID.Parse(caseVariant, factory, CancellationToken.None);
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
        var archivePath = await CreateArchive("test.7z", ("folder/first.txt", "first"));
        var id = await SevenZipID.Parse(
            ProviderID(archivePath, "folder"),
            new SevenZipProviderFactory(),
            CancellationToken.None);
        try {
            Assert.That(id, Is.Not.Null);
            Assert.That(id.RelativePath.Original, Is.EqualTo("folder"));
        }
        finally {
            id?.Release();
        }
    }

    [TestCase("assets.7z")]
    [TestCase("assets.7z/subfolder")]
    public async Task Parse_ReturnsTheIdentityOfDirectoriesWithSevenZipSuffix(string relativePath) {
        var archivePath = await CreateArchive("test.7z", ("assets.7z/subfolder/first.txt", "first"));
        var id = await SevenZipID.Parse(
            ProviderID(archivePath, relativePath),
            new SevenZipProviderFactory(),
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
        var archivePath = await CreateArchive("test.7z", ("first.txt", "first"));
        var id = await SevenZipID.Parse(
            ProviderID(archivePath, "first.txt"),
            new SevenZipProviderFactory(),
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
        var archivePath = await CreateArchive("test.7z", ("first.txt", "first"));
        var id = await SevenZipID.Parse(
            ProviderID(archivePath, "missing"),
            new SevenZipProviderFactory(),
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
        var id = await SevenZipID.Parse(file, new SevenZipProviderFactory(), CancellationToken.None);
        Assert.That(id, Is.Null);
    }

    [Test]
    public async Task Parse_IsNullForAFileThatDoesNotExist() {
        var id = await SevenZipID.Parse(
            Sandbox.PathFor("missing.7z"),
            new SevenZipProviderFactory(),
            CancellationToken.None);
        Assert.That(id, Is.Null);
    }

    [Test]
    public async Task Parse_IsNullForAnEmptyId() {
        var id = await SevenZipID.Parse("", new SevenZipProviderFactory(), CancellationToken.None);
        Assert.That(id, Is.Null);
    }

    [Test]
    public async Task Parse_ReturnsTheIdentityOfAnArchiveThatIsInsideAnArchive() {
        var innerPath = await CreateArchive("inner.7z", ("inside.txt", "nested"));
        var archivePath = Sandbox.PathFor("test.7z");
        await SevenZipArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "inner.7z", innerPath }
        }, null, null, CancellationToken.None);
        var id = await SevenZipID.Parse(
            ProviderID(archivePath, "", "inner.7z"),
            new SevenZipProviderFactory(),
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
        var innerPath = await CreateArchive("inner.7z", ("deep/inside.txt", "nested"));
        var archivePath = Sandbox.PathFor("test.7z");
        await SevenZipArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "inner.7z", innerPath }
        }, null, null, CancellationToken.None);
        var id = await SevenZipID.Parse(
            ProviderID(archivePath, "deep", "inner.7z"),
            new SevenZipProviderFactory(),
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
        Sandbox.WriteFile("inner.7z", "not an archive");
        var archivePath = await CreateArchive("test.7z", ("first.txt", "first"));
        var id = await SevenZipID.Parse(
            ProviderID(archivePath, "", "inner.7z"),
            new SevenZipProviderFactory(),
            CancellationToken.None);
        Assert.That(id, Is.Null);
    }

    [Test]
    public async Task Parent_IsThePlaceInsideTheOuterArchiveForTheRootOfANestedArchive() {
        var innerPath = await CreateArchive("inner.7z", ("inside.txt", "nested"));
        var archivePath = Sandbox.PathFor("test.7z");
        await SevenZipArchiveFile.Create(archivePath, new Dictionary<string, string> {
            { "a>b/inner.7z", innerPath }
        }, null, null, CancellationToken.None);
        var id = await SevenZipID.Parse(
            ProviderID(archivePath, "", "a>b/inner.7z"),
            new SevenZipProviderFactory(),
            CancellationToken.None);
        try {
            var parentID = id.Parent;
            var parent = await SevenZipID.Parse(parentID, new SevenZipProviderFactory(), CancellationToken.None);
            try {
                var parentEntries = await parent.EntryInfo(CancellationToken.None);
                using (Assert.EnterMultipleScope()) {
                    Assert.That(parentID, Is.EqualTo(ProviderID(archivePath, "a>b")));
                    Assert.That(parent, Is.Not.Null);
                    Assert.That(parent?.ArchivePath.Nest.Count, Is.Zero);
                    Assert.That(parent?.RelativePath.Normalized, Is.EqualTo("a>b"));
                    Assert.That(parentEntries.Select(item => item.Name.Original), Is.EquivalentTo(new[] {
                        "a>b/inner.7z"
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
        var archivePath = await CreateArchive("test.7z", ("first.txt", "first"), ("folder/second.txt", "second"));
        var id = await SevenZipID.Parse(archivePath, new SevenZipProviderFactory(), CancellationToken.None);
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
        var archivePath = await CreateArchive("test.7z", ("first.txt", "first"), ("folder/second.txt", "second"));
        var id = await SevenZipID.Parse(
            ProviderID(archivePath, "folder"),
            new SevenZipProviderFactory(),
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
        var archivePath = await CreateArchive("test.7z", ("first.txt", "first"));
        var id = await SevenZipID.Parse(archivePath, new SevenZipProviderFactory(), CancellationToken.None);
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
        var archivePath = await CreateArchive("test.7z", new Dictionary<string, string> {
            { "a>b/sub/first.txt", source }
        });
        var id = await SevenZipID.Parse(
            ProviderID(archivePath, "a>b/sub"),
            new SevenZipProviderFactory(),
            CancellationToken.None);
        try {
            var parentID = id.Parent;
            var parent = await SevenZipID.Parse(parentID, new SevenZipProviderFactory(), CancellationToken.None);
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
        var archivePath = await CreateArchive("delimiter-directory.7z", new Dictionary<string, string> {
            { "a>b/inside.txt", insidePath }
        });
        var providerID = ProviderID(archivePath, "a>b");
        var id = await SevenZipID.Parse(providerID, new SevenZipProviderFactory(), CancellationToken.None);

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
        var innerPath = await CreateArchive("inner-source.7z", ("inside.txt", "nested contents"));
        var archivePath = await CreateArchive("outer.7z", new Dictionary<string, string> {
            { "a>b.7z", innerPath },
            { "a>b-directory/inside.txt", Sandbox.WriteFile("outer-inside.txt", "outer contents") }
        });
        var providerID = ProviderID(archivePath, "", "a>b.7z");
        var id = await SevenZipID.Parse(providerID, new SevenZipProviderFactory(), CancellationToken.None);

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
    public async Task Parse_TreatsDelimiterBearingMembersAsOneMemberAtEveryNestingLevel() {
        var deepPath = await CreateArchive("deep-source.7z", ("deep/inside.txt", "deep contents"));
        var middlePath = await CreateArchive("middle-source.7z", new Dictionary<string, string> {
            { "inner>deep.7z", deepPath }
        });
        var archivePath = await CreateArchive("two-level.7z", new Dictionary<string, string> {
            { "outer>middle.7z", middlePath }
        });
        var providerID = ProviderID(archivePath, "deep", "outer>middle.7z", "inner>deep.7z");
        var id = await SevenZipID.Parse(providerID, new SevenZipProviderFactory(), CancellationToken.None);

        try {
            Assert.That(id, Is.Not.Null);
            var entries = await id.EntryInfo(CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(id.ArchivePath.Nest.Count, Is.EqualTo(2));
                Assert.That(id.RelativePath.Normalized, Is.EqualTo("deep"));
                Assert.That(entries.Select(entry => entry.Name.Original), Is.EquivalentTo(new[] {
                    "deep/inside.txt"
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
        var archivePath = await CreateArchive("explicit-fields.7z", new Dictionary<string, string> {
            { "assets.7z/inside.txt", directorySource },
            { "ordinary.txt", Sandbox.WriteFile("ordinary-source.txt", "file") }
        });
        var factory = new SevenZipProviderFactory();
        var directory = await SevenZipID.Parse(
            ProviderID(archivePath, "assets.7z"),
            factory,
            CancellationToken.None);
        var nestedDirectory = await SevenZipID.Parse(
            ProviderID(archivePath, "", "assets.7z"),
            factory,
            CancellationToken.None);
        var fileDirectory = await SevenZipID.Parse(
            ProviderID(archivePath, "ordinary.txt"),
            factory,
            CancellationToken.None);

        try {
            using (Assert.EnterMultipleScope()) {
                Assert.That(directory, Is.Not.Null);
                Assert.That(directory?.RelativePath.Normalized, Is.EqualTo("assets.7z"));
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
        var archivePath = await CreateArchive("malformed-prefix.7z", ("keep.txt", "contents"));
        var id = await SevenZipID.Parse(
            $"7z:1|f={archivePath}|d=%Q0",
            new SevenZipProviderFactory(),
            CancellationToken.None);

        Assert.That(id, Is.Null);
    }

    [Test]
    public async Task Parse_IsNullForAnIDBelongingToAnotherArchiveProvider() {
        var archivePath = await CreateArchive("wrong-provider.7z", ("keep.txt", "contents"));
        var id = await SevenZipID.Parse(
            $"zip:1|f={archivePath}|d=",
            new SevenZipProviderFactory(),
            CancellationToken.None);

        Assert.That(id, Is.Null);
    }

    [Test]
    public async Task Parse_IsNullForASevenZipIDWithANonSevenZipOuterFile() {
        var archivePath = Sandbox.WriteFile("wrong-extension.zip", "not a SevenZip archive");
        var id = await SevenZipID.Parse(
            ProviderID(archivePath, ""),
            new SevenZipProviderFactory(),
            CancellationToken.None);

        Assert.That(id, Is.Null);
    }

    [Test]
    public async Task Parse_IsNullForARelativeArchivePathInAStructuredID() {
        var id = await SevenZipID.Parse(
            "7z:1|f=relative.7z|d=",
            new SevenZipProviderFactory(),
            CancellationToken.None);

        Assert.That(id, Is.Null);
    }

    [Test]
    public async Task Parse_RejectsLegacyCompositeIDs() {
        var archivePath = await CreateArchive("legacy-id.7z", ("folder/inside.txt", "contents"));
        var id = await SevenZipID.Parse(
            $"{archivePath}>folder",
            new SevenZipProviderFactory(),
            CancellationToken.None);

        Assert.That(id, Is.Null);
    }
}
