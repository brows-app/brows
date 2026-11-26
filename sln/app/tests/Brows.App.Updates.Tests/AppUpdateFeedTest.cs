using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
internal sealed class AppUpdateFeedTest {
    [TestCase(null, null, true, "0.103.0.60", typeof(ArgumentNullException))]
    [TestCase("amd64", null, false, "0.103.0.60", typeof(ArgumentException))]
    [TestCase("ARM64", null, false, "0.103.0.60", typeof(ArgumentException))]
    [TestCase(null, "0.104.0", false, "0.103.0.60", typeof(ArgumentException))]
    [TestCase(null, null, false, "invalid", typeof(InvalidOperationException))]
    public void Get_RejectsInvalidRequestsBeforeNetwork(
        string architecture, string version, bool nullRequest, string running, Type exception) {
        using (var directory = new UpdateTestDirectory())
        using (var handler = new UpdateHttpHandler(_ => UpdateHttpHandler.Json(Array.Empty<object>())))
        using (var http = new HttpClient(handler)) {
            IAppUpdateAvailable feed = new AppUpdateFeed {
                Http = http,
                Installation = directory.Installation(version: running),
                Repository = "owner/name"
            };
            var request = nullRequest ? null : new AppRequest { Architecture = architecture, Version = version };
            Assert.That(async () => await feed.Get(request, CancellationToken.None), Throws.TypeOf(exception));
            Assert.That(handler.Requests, Is.Empty);
        }
    }

    [Test]
    public async Task Get_UsesRequestedArm64OnArm64AndChecksEachTime() {
        using (var directory = new UpdateTestDirectory())
        using (var handler = new UpdateHttpHandler(_ => UpdateHttpHandler.Json(new[] { new {
                   tag_name = "v0.104.0.61",
                   html_url = "https://github.com/owner/name/releases/tag/v0.104.0.61",
                   assets = Array.Empty<object>()
               } })))
        using (var http = new HttpClient(handler)) {
            IAppUpdateAvailable feed = new AppUpdateFeed {
                Http = http, Installation = directory.Installation(Architecture.Arm64), Repository = "owner/name"
            };
            var request = new AppRequest { Architecture = "ARM64" };
            var first = await feed.Get(request, CancellationToken.None);
            var second = await feed.Get(request, CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(first.Architecture, Is.EqualTo("arm64"));
                Assert.That(second, Is.EqualTo(first));
                Assert.That(handler.Requests, Has.Count.EqualTo(2));
            }
        }
    }
}
