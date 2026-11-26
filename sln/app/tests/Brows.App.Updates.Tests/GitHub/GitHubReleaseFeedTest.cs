using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Brows.GitHub;

[TestFixture]
internal sealed class GitHubReleaseFeedTest {
    private const string Stable = "0.104.0.61";
    private const string Beta = "0.105.0-beta.62";
    private const string Later = "0.105.0.63";
    private const string Current = "0.103.0.60";

    private static object Release(string version, string architectures, bool draft = false, bool preRelease = false,
                                  string digest = "valid", string scheme = "https") {
        return new {
            tag_name = "v" + version,
            html_url = "https://github.com/owner/name/releases/tag/v" + version,
            published_at = "2026-11-25T23:50:00Z",
            draft,
            prerelease = preRelease,
            assets = architectures.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(arch => {
                var name = $"brows-{arch}-{version}.msi";
                return new {
                    name,
                    browser_download_url = $"{scheme}://github.com/owner/name/releases/download/v{version}/{name}",
                    size = 100,
                    digest = digest == "valid" ? "sha256:" + new string('A', 64) : digest
                };
            })
        };
    }

    private static GitHubReleaseFeed Feed(HttpClient http) {
        Assert.That(AppVersion.TryParse(Current, out var version), Is.True);
        return new(http, version, "owner/name");
    }

    [TestCase("x64", "x64,arm64", "x64,arm64", Later, "x64")]
    [TestCase("arm64", "x64,arm64", "x64,arm64", Later, "arm64")]
    [TestCase("arm64", "arm64", "x64", Stable, "arm64")]
    [TestCase("arm64", "x64", "x64", Later, null)]
    [TestCase(null, "arm64", "x64", Later, null)]
    public async Task Find_SelectsNewestReleaseWithMatchingArchitecture(
        string architecture, string olderAssets, string newerAssets, string version, string packageArch) {
        using (var handler = new UpdateHttpHandler(_ => UpdateHttpHandler.Json(new[] {
                   Release(Stable, olderAssets), Release(Later, newerAssets)
               })))
        using (var http = new HttpClient(handler)) {
            var result = await Feed(http).Find(architecture, null, false, CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(result.Version, Is.EqualTo(version));
                Assert.That(result.Architecture, Is.EqualTo(architecture));
                Assert.That(result.Package?.Name,
                    Is.EqualTo(packageArch is null ? null : $"brows-{packageArch}-{version}.msi"));
                Assert.That(handler.Requests.Single().PathAndQuery, Is.EqualTo("/repos/owner/name/releases?per_page=100"));
            }
        }
    }

    [TestCase(false, false, Stable)]
    [TestCase(true, false, Beta)]
    [TestCase(true, true, Later)]
    public async Task Find_FiltersDraftsUnparseableTagsOlderReleasesAndUnrequestedPrereleases(
        bool allowPreRelease, bool addLaterStable, string expectedVersion) {
        object[] releases = [
            Release("nonsense", "x64"), Release("0.106.0.64", "x64", draft: true),
            Release(Beta, "x64", preRelease: true), Release(Stable, "x64"),
            Release(Current, "x64"), Release("0.102.0.59", "x64")
        ];
        if (addLaterStable) {
            releases = [.. releases, Release(Later, "x64")];
        }

        using (var http = new HttpClient(new UpdateHttpHandler(_ => UpdateHttpHandler.Json(releases)))) {
            var result = await Feed(http).Find("x64", null, allowPreRelease, CancellationToken.None);
            Assert.That(result.Version, Is.EqualTo(expectedVersion));
        }
    }

    [TestCase("x64", null, "https")]
    [TestCase("x64", "sha256:invalid", "https")]
    [TestCase("x64", "valid", "http")]
    [TestCase("", "valid", "https")]
    [TestCase("arm64", "valid", "https")]
    public async Task Find_ReturnsReleaseWithoutAnEligiblePackage(string architectures, string digest, string scheme) {
        using (var http = new HttpClient(new UpdateHttpHandler(_ =>
                   UpdateHttpHandler.Json(new[] { Release(Stable, architectures, digest: digest, scheme: scheme) })))) {
            var result = await Feed(http).Find("x64", null, false, CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(result.Version, Is.EqualTo(Stable));
                Assert.That(result.Package, Is.Null);
            }
        }
    }

    [TestCase(Stable, Stable, false, false, "x64", true, true)]
    [TestCase(Beta, Beta, true, false, "x64", true, true)]
    [TestCase(Beta, Beta, true, true, "x64", false, false)]
    [TestCase(Beta, "0.105.0-rc.62", true, false, "x64", false, false)]
    [TestCase(Stable, Stable, false, false, "arm64", true, false)]
    public async Task Find_PinRequiresMatchingPublishedTagAndOverridesPrereleaseSetting(
        string pinText, string tagVersion, bool preRelease, bool draft, string assets, bool found, bool package) {
        using (var handler = new UpdateHttpHandler(_ =>
                   UpdateHttpHandler.Json(Release(tagVersion, assets, draft, preRelease))))
        using (var http = new HttpClient(handler)) {
            Assert.That(AppVersion.TryParse(pinText, out var pin), Is.True);
            var result = await Feed(http).Find("x64", pin, false, CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(handler.Requests.Single().AbsolutePath, Is.EqualTo($"/repos/owner/name/releases/tags/v{pinText}"));
                Assert.That(result?.Version, Is.EqualTo(found ? pinText : null));
                Assert.That(result?.Package is not null, Is.EqualTo(package));
                Assert.That(result?.PreRelease, Is.EqualTo(found ? preRelease : (bool?)null));
            }
        }
    }

    [TestCase("0.103.0.60", 0)]
    [TestCase("0.102.0.59", 0)]
    [TestCase(Beta, 1)]
    public async Task Find_PinOlderEqualOrMissingReturnsNull(string pinText, int requests) {
        using (var handler = new UpdateHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)))
        using (var http = new HttpClient(handler)) {
            Assert.That(AppVersion.TryParse(pinText, out var pin), Is.True);
            Assert.That(await Feed(http).Find("x64", pin, false, CancellationToken.None), Is.Null);
            Assert.That(handler.Requests, Has.Count.EqualTo(requests));
        }
    }

    [Test]
    public async Task Find_SendsGitHubHeadersAndReadsAssetMetadata() {
        using (var http = new HttpClient(new UpdateHttpHandler(request => {
                   using (Assert.EnterMultipleScope()) {
                       Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
                       Assert.That(request.Headers.UserAgent.ToString(), Is.EqualTo($"Brows/{Current}"));
                       Assert.That(request.Headers.Accept.Single().MediaType, Is.EqualTo("application/vnd.github+json"));
                       Assert.That(request.Headers.GetValues("X-GitHub-Api-Version"), Is.EqualTo(new[] { "2022-11-28" }));
                   }
                   return UpdateHttpHandler.Json(new[] { Release(Stable, "X64") });
               }))) {
            var result = await Feed(http).Find("x64", null, false, CancellationToken.None);
            using (Assert.EnterMultipleScope()) {
                Assert.That(result.Page.AbsolutePath, Is.EqualTo($"/owner/name/releases/tag/v{Stable}"));
                Assert.That(result.Published, Is.EqualTo(new DateTimeOffset(2026, 11, 25, 23, 50, 0, TimeSpan.Zero)));
                Assert.That(result.Package.Size, Is.EqualTo(100));
                Assert.That(result.Package.Sha256, Is.EqualTo(new string('a', 64)));
                Assert.That(result.Package.Name, Is.EqualTo($"brows-x64-{Stable}.msi"));
            }
        }
    }

    [TestCase(HttpStatusCode.Forbidden, typeof(HttpRequestException))]
    [TestCase(HttpStatusCode.OK, typeof(JsonException))]
    public void Find_PropagatesHttpAndJsonErrors(HttpStatusCode status, Type error) {
        using (var http = new HttpClient(new UpdateHttpHandler(_ =>
                   new HttpResponseMessage(status) { Content = new StringContent("{") }))) {
            Assert.That(async () => await Feed(http).Find("x64", null, false, CancellationToken.None),
                Throws.InstanceOf(error));
        }
    }
}
