using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IPC.Client;

[TestFixture]
internal sealed class ClientStreamTest {
    class Impl : ClientStream {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing) {
            Disposed = disposing;
        }

        public override Task<string> Read(CancellationToken token) {
            throw new NotImplementedException();
        }

        public override Task Write(string value, CancellationToken token) {
            throw new NotImplementedException();
        }
    }

    [Test]
    public void Dispose_CallsProtectedDispose() {
        var subject = new Impl();
        subject.Dispose();
        Assert.That(subject.Disposed, Is.True);
    }
}
