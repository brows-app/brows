using Brows.Tests;

namespace Brows;

[TestFixture]
public sealed class FileProtocolEntryTest {
    private TestProvider Provider;

    [SetUp]
    public void SetUp() {
        Provider = new TestProvider("ftp://host/root/");
    }

    [TearDown]
    public void TearDown() {
        Provider?.Dispose();
        Provider = null;
    }

    [Test]
    public void ID_IsTheUriString() {
        var entry = new TestEntry(Provider, "ftp://host/root/file.txt");
        Assert.That(entry.ID, Is.EqualTo("ftp://host/root/file.txt"));
    }

    [Test]
    public void ID_IsTheSameInstanceOnEachAccess() {
        var entry = new TestEntry(Provider, "ftp://host/root/file.txt");
        var firstId = entry.ID;
        Assert.That(entry.ID, Is.SameAs(firstId));
    }

    [Test]
    public void ToString_IsTheID() {
        var entry = new TestEntry(Provider, "ftp://host/root/file.txt");
        Assert.That(entry.ToString(), Is.EqualTo(entry.ID));
    }

    [Test]
    public void Name_DefaultsToTheID() {
        var entry = new TestEntry(Provider, "ftp://host/root/file.txt");
        Assert.That(entry.Name, Is.EqualTo(entry.ID));
    }

    [Test]
    public void Kind_IsTheConstructedKind() {
        var entry = new TestEntry(Provider, "ftp://host/root/dir/", FileProtocolEntryKind.Directory);
        Assert.That(entry.Kind, Is.EqualTo(FileProtocolEntryKind.Directory));
    }

    [TestCase("file.txt", ".txt")]
    [TestCase("file.tar.gz", ".gz")]
    [TestCase("file", "")]
    [TestCase("", "")]
    [TestCase("file.", "")]
    [TestCase(".gitignore", ".gitignore")]
    [TestCase("file.TXT", ".TXT")]
    [TestCase("file name.txt", ".txt")]
    public void Extension_IsTheTextFromTheLastDot(string name, string expected) {
        var entry = new TestEntry(Provider, "ftp://host/root/x") { NameValue = name };
        Assert.That(entry.Extension, Is.EqualTo(expected));
    }

    [TestCase("dir.d/file", "")]
    [TestCase("dir.d/file.txt", ".txt")]
    [TestCase("dir.d/", "")]
    public void Extension_StopsAtAPathSeparator(string name, string expected) {
        var entry = new TestEntry(Provider, "ftp://host/root/x") { NameValue = name };
        Assert.That(entry.Extension, Is.EqualTo(expected));
    }

    [TestCase("file.txt")]
    [TestCase("file")]
    [TestCase("file.")]
    [TestCase("dir.d/file")]
    [TestCase(".gitignore")]
    public void Extension_MatchesPathGetExtension(string name) {
        var entry = new TestEntry(Provider, "ftp://host/root/x") { NameValue = name };
        Assert.That(entry.Extension, Is.EqualTo(System.IO.Path.GetExtension(name)));
    }

    [Test]
    public void Extension_IsDerivedFromNameNotFromTheUri() {
        var entry = new TestEntry(Provider, "ftp://host/root/file.txt") { NameValue = "file.dat" };
        Assert.That(entry.Extension, Is.EqualTo(".dat"));
    }

    [Test]
    public void Extension_OfTheDefaultNameIsTakenFromTheLastUriSegment() {
        // Name is not overridden, so it falls back to ID (the full uri), but the
        // extension search stops at the last separator, so the host is not included.
        var entry = new TestEntry(Provider, "ftp://host.name/root/file");
        Assert.That(entry.Extension, Is.EqualTo(""));
    }

    [Test]
    public void Extension_OfTheDefaultNameUsesTheLastUriSegment() {
        var entry = new TestEntry(Provider, "ftp://host.name/root/file.txt");
        Assert.That(entry.Extension, Is.EqualTo(".txt"));
    }

    [Test]
    public void Extension_IsTheSameInstanceOnEachAccess() {
        var entry = new TestEntry(Provider, "ftp://host/root/x") { NameValue = "file.txt" };
        var firstExtension = entry.Extension;
        Assert.That(entry.Extension, Is.SameAs(firstExtension));
    }

    [Test]
    public void Constructor_ThrowsWhenProviderIsNull() {
        Assert.Throws<ArgumentNullException>(() => new TestEntry(null, "ftp://host/root/x"));
    }
}
