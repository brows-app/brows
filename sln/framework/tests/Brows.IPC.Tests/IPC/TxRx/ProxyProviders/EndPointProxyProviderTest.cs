using System.Linq;
using System.Net;

namespace Brows.IPC.TxRx.ProxyProviders;

[TestFixture]
internal sealed class EndPointProxyProviderTest {
    [Test]
    public void Constructor_FiltersNullEndPoints() {
        var typeResolver = ObjectTypeResolver.Lookup(typeof(string));
        var endPoint = new IPEndPoint(IPAddress.Loopback, 12345);

        var subject = new EndPointProxyProvider(typeResolver, [null, endPoint, null]);

        using (Assert.EnterMultipleScope()) {
            Assert.That(subject.TypeResolver, Is.SameAs(typeResolver));
            Assert.That(subject.EndPoints, Is.EqualTo(new[] { endPoint }));
        }
    }

    [Test]
    public void Constructor_NullEndPoints_UsesEmptyEndPoints() {
        var subject = new EndPointProxyProvider(ObjectTypeResolver.Lookup(typeof(string)), null);

        Assert.That(subject.EndPoints, Is.Empty);
    }

    [Test]
    public void Start_RaisesProxiesChanged() {
        var subject = new EndPointProxyProvider(ObjectTypeResolver.Lookup(typeof(string)), []);
        object sender = null;
        EventArgs args = null;

        subject.ProxiesChanged += (s, e) => {
            sender = s;
            args = e;
        };
        subject.Start();

        using (Assert.EnterMultipleScope()) {
            Assert.That(sender, Is.SameAs(subject));
            Assert.That(args, Is.SameAs(EventArgs.Empty));
        }
    }

    [Test]
    public void GetProxies_ReturnsProxyForEachEndPoint() {
        var typeResolver = ObjectTypeResolver.Lookup(typeof(string));
        var endPoint1 = new IPEndPoint(IPAddress.Loopback, 1111);
        var endPoint2 = new IPEndPoint(IPAddress.Loopback, 2222);
        var subject = new EndPointProxyProvider(typeResolver, [endPoint1, endPoint2]);

        var actual = subject.GetProxies().ToList();

        using (Assert.EnterMultipleScope()) {
            Assert.That(actual, Has.Count.EqualTo(2));
            Assert.That(actual.Select(proxy => proxy.ToString()), Is.EqualTo(new[] { endPoint1.ToString(), endPoint2.ToString() }));
        }
    }
}
