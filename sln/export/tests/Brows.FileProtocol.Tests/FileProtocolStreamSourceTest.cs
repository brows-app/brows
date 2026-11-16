using Brows.Tests;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
public sealed class FileProtocolStreamSourceTest {
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
    public void Constructor_ThrowsWhenTheEntryIsNull() {
        Assert.Throws<ArgumentNullException>(() => new TestStreamSource(null));
    }

    [Test]
    public void EntryID_IsTheEntryID() {
        var entry = new TestEntry(Provider, "ftp://host/root/file.txt");
        var subject = new TestStreamSource(entry);
        Assert.That(((IEntryStreamSource)subject).EntryID, Is.EqualTo(entry.ID));
    }

    [Test]
    public void EntryName_IsTheEntryName() {
        var entry = new TestEntry(Provider, "ftp://host/root/file.txt") { NameValue = "file.txt" };
        var subject = new TestStreamSource(entry);
        Assert.That(((IEntryStreamSource)subject).EntryName, Is.EqualTo("file.txt"));
    }

    [Test]
    public void SourceFile_IsNull() {
        var subject = new TestStreamSource(new TestEntry(Provider, "ftp://host/root/file.txt"));
        Assert.That(((IEntryStreamSource)subject).SourceFile, Is.Null);
    }

    [Test]
    public void SourceDirectory_IsNull() {
        var subject = new TestStreamSource(new TestEntry(Provider, "ftp://host/root/file.txt"));
        Assert.That(((IEntryStreamSource)subject).SourceDirectory, Is.Null);
    }

    [Test]
    public void Stream_IsNullBeforeItIsReady() {
        var subject = new TestStreamSource(new TestEntry(Provider, "ftp://host/root/file.txt"));
        Assert.That(((IEntryStreamSource)subject).Stream, Is.Null);
    }

    [Test]
    public async Task StreamReady_IsNotNullByDefault() {
        var subject = new TestStreamSource(new TestEntry(Provider, "ftp://host/root/file.txt"));
        var ready = await ((IEntryStreamSource)subject).StreamReady(default);
        Assert.That(ready, Is.Not.Null);
    }
}
