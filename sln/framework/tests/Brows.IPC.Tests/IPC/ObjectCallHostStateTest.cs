using NUnit.Framework;
using System;
using System.Net;

namespace Brows.IPC;

[TestFixture]
public sealed class ObjectCallHostStateTest {
    [Test]
    public void Constructor_SetsEndPoint() {
        var ip = IPAddress.Parse("127.0.0.1");
        var port = 12345;
        var endPoint = new IPEndPoint(ip, port);

        var state = new ObjectCallHostState(endPoint);

        Assert.That(state.EndPoint, Is.EqualTo(endPoint));
    }

    [Test]
    public void Constructor_ThrowsOnNullEndPoint() {
        Assert.Throws<ArgumentNullException>(() => new ObjectCallHostState(null));
    }

    [Test]
    public void Port_ReturnsEndPointPort() {
        var ip = IPAddress.Loopback;
        var port = 54321;
        var endPoint = new IPEndPoint(ip, port);

        var state = new ObjectCallHostState(endPoint);

        Assert.That(state.Port, Is.EqualTo(port));
    }
}