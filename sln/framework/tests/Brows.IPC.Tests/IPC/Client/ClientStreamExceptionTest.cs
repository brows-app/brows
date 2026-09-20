using NUnit.Framework;
using Brows.IPC.Client.ClientStreamProviders;
using System.IO;

namespace Brows.IPC.Client;

[TestFixture]
internal class ClientStreamExceptionTest {
    [Test]
    public void Constructor_SetsProvider() {
        using (var s = new MemoryStream()) {
            var expected = new ExistingStreamProvider(s, false);
            var subject = new ClientStreamException(expected, null);
            Assert.That(subject.Provider, Is.SameAs(expected));
        }
    }

    [Test]
    public void Constructor_SetsInnerException() {
        var expected = new IOException();
        using (var s = new MemoryStream()) {
            var provider = new ExistingStreamProvider(s, false);
            var subject = new ClientStreamException(provider, expected);
            Assert.That(subject.InnerException, Is.SameAs(expected));
        }
    }
}
