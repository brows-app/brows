using NUnit.Framework;
using System;
using System.IO;

namespace Brows.IPC.Client.ClientStreamProviders;

[TestFixture]
internal sealed class ExistingStreamProviderTest {
    [Test]
    public void Constructor_SetsStream() {
        using (var s = new MemoryStream()) {
            var subject = new ExistingStreamProvider(s, false);
            Assert.That(subject.ExistingStream, Is.SameAs(s));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Constructor_SetsDisposeStream(bool value) {
        using (var s = new MemoryStream()) {
            var subject = new ExistingStreamProvider(s, value);
            Assert.That(subject.DisposeStream, Is.EqualTo(value));
        }
    }

    [Test]
    public void Dispose_DisposesStreamIfFlagSet() {
        var s = new MemoryStream(new byte[1024]);
        var err = default(Exception);
        var subject = new ExistingStreamProvider(s, disposeStream: true);
        subject.Dispose();
        try {
            s.Write([1], 0, 1);
        }
        catch (Exception ex) {
            err = ex;
        }
        Assert.That(err, Is.InstanceOf<ObjectDisposedException>());
    }

    [Test]
    public void Dispose_DoesNotDisposesStreamIfFlagUnset() {
        var s = new MemoryStream(new byte[1024]);
        try {
            var err = default(Exception);
            var subject = new ExistingStreamProvider(s, disposeStream: false);
            subject.Dispose();
            try {
                s.Write([1], 0, 1);
            }
            catch (Exception ex) {
                err = ex;
            }
            Assert.That(err, Is.Null);
        }
        finally {
            s.Dispose();
        }
    }
}
