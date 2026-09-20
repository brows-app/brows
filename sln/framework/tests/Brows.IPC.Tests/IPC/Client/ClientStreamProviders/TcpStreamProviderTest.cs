using NUnit.Framework;
using System.Net;

namespace Brows.IPC.Client.ClientStreamProviders;

[TestFixture]
internal sealed class TcpStreamProviderTest {
    [Test]
    public void ToString_ReturnsEndPoint() {
        var endPoint = new IPEndPoint(IPAddress.Parse("45.32.11.1"), 21);
        var subject = new TcpStreamProvider(endPoint);
        var expected = "45.32.11.1:21";
        Assert.That(subject.ToString(), Is.EqualTo(expected));
    }
}
