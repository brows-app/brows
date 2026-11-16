using Brows.DirectoryListing;
using Brows.Entries;
using System;

namespace Brows;

[TestFixture]
internal sealed class FtpStreamGuiTest {
    private TestProvider Provider;

    [SetUp]
    public void SetUp() {
        Provider = new TestProvider(new Uri("ftp://example.test/root/"));
    }

    [TearDown]
    public void TearDown() {
        Provider?.Dispose();
        Provider = null;
    }

    private FtpEntry Entry(string name, long? size) {
        return new FtpEntry(Provider, new DirectoryListItem(
            name,
            size,
            null,
            null,
            null,
            null,
            0));
    }

    [Test]
    public void Stream_UsesTheSelectedEntryAndConfiguredReadOnlyPreviewOptions() {
        var first = Entry("first.txt", 5);
        var selected = Entry("selected.txt", 8);
        var stream = selected.Stream;
        var source = (IEntryStreamSource)stream.Source;

        using (Assert.EnterMultipleScope()) {
            Assert.That(stream.ForceText, Is.True);
            Assert.That(stream.ForceImage, Is.False);
            Assert.That(Provider.StreamOptions.Text.SourceLengthMax, Is.EqualTo(1000000));
            Assert.That(Provider.StreamOptions.Image.SourceLengthMax, Is.EqualTo(10000000));
            Assert.That(source.EntryID, Is.EqualTo(selected.ID));
            Assert.That(source.EntryID, Is.Not.EqualTo(first.ID));
            Assert.That(source.RelativePath, Is.EqualTo("selected.txt"));
            Assert.That(source.StreamLength, Is.EqualTo(8));
            Assert.That(source.SourceFile, Is.Null);
        }
    }

    [Test]
    public void Stream_UsesTheConfiguredImageLengthLimit() {
        var entry = Entry("photo.png", 42);
        Provider.StreamOptions.Image.SourceLengthMax = 1024;

        var stream = entry.Stream;
        var source = (IEntryStreamSource)stream.Source;

        using (Assert.EnterMultipleScope()) {
            Assert.That(stream.ForceText, Is.False);
            Assert.That(stream.ForceImage, Is.True);
            Assert.That(Provider.StreamOptions.Image.SourceLengthMax, Is.EqualTo(1024));
            Assert.That(source.StreamLength, Is.EqualTo(42));
        }
    }

    [Test]
    public void Stream_LinkDoesNotAdvertiseLinkMetadataAsTargetLength() {
        var entry = new FtpEntry(Provider, new DirectoryListItem(
            "target-link.txt",
            4,
            null,
            null,
            null,
            null,
            DirectoryListItemFlags.Link));
        var source = (IEntryStreamSource)entry.Stream.Source;

        Assert.That(source.StreamLength, Is.Zero);
    }

    private sealed class TestProvider : FtpProviderBase {
        public TestProvider(Uri uri) : base(new TestFactory(), uri, null) {
        }
    }

    private sealed class TestFactory : IFtpProviderFactory {
        public FtpClientCache Client { get; } = new();
    }
}
