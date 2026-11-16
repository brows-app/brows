using Brows.SFTP.Native;
using System;

namespace Brows;

[TestFixture]
internal sealed class SftpStreamSourceTest {
    [TestCase(4u, 0ul, -1L)]
    [TestCase(5u, 0ul, 0L)]
    [TestCase(5u, 12ul, 12L)]
    [TestCase(5u, ulong.MaxValue, -1L)]
    public void StreamLength_PreservesUnknownSize(uint flags, ulong size, long expected) {
        var factory = new SftpProviderFactory();
        var uri = new Uri("sftp://user@localhost/root/");
        using var lease = factory.Acquire(uri);
        using var provider = new SftpProvider(factory, uri, null, lease);
        var metadata = new SftpMetadata("file", flags, size, 0, 0, 0x81A4, 0, 0);
        var entry = new SftpEntry(provider, metadata, FileProtocolEntryKind.File);
        var source = new SftpStreamSource(entry);
        Assert.That(source.StreamLength, Is.EqualTo(expected));
    }
}
