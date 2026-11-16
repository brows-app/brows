using Brows.SFTP.Native;
using Brows.Entries;
using System;

namespace Brows;

[TestFixture]
internal sealed class SftpStreamGuiTest {
    private SftpProviderFactory Factory;
    private SftpClientLease Lease;
    private SftpProvider Provider;

    [SetUp]
    public void SetUp() {
        Factory = new SftpProviderFactory();
        var uri = new Uri("sftp://user@localhost/root/");
        Lease = Factory.Acquire(uri);
        Provider = new SftpProvider(Factory, uri, null, Lease);
    }

    [TearDown]
    public void TearDown() {
        Provider?.Dispose();
        Provider = null;
        Lease?.Dispose();
        Lease = null;
        Factory = null;
    }

    private SftpEntry Entry(string name, uint flags, ulong size) {
        var metadata = new SftpMetadata(name, flags, size, 0, 0, 0x81A4, 0, 0);
        return new SftpEntry(Provider, metadata, FileProtocolEntryKind.File);
    }

    private SftpEntry Link(string name, ulong size) {
        var metadata = new SftpMetadata(name, 1 | 4, size, 0, 0, 0xA1FF, 0, 0);
        return new SftpEntry(Provider, metadata, FileProtocolEntryKind.FileLink);
    }

    [Test]
    public void Stream_UsesTheSelectedEntryAndReadOnlyPreviewOptions() {
        var first = Entry("first.txt", 1 | 4, 5);
        var selected = Entry("selected.txt", 1 | 4, 8);
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
    public void Stream_UsesAReadLimitWhenRemoteMetadataHasNoSize() {
        var entry = Entry("unknown.txt", 4, 0);
        var stream = entry.Stream;
        var source = (IEntryStreamSource)stream.Source;

        using (Assert.EnterMultipleScope()) {
            Assert.That(stream.ForceText, Is.True);
            Assert.That(source.StreamLength, Is.EqualTo(0));
            Assert.That(Provider.StreamOptions.Text.SourceLengthMax, Is.EqualTo(1000000));
        }
    }

    [Test]
    public void Stream_LinkDoesNotAdvertiseLinkMetadataAsTargetLength() {
        var entry = Link("target-link.txt", 4);
        var source = (IEntryStreamSource)entry.Stream.Source;

        Assert.That(source.StreamLength, Is.Zero);
    }
}
