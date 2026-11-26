using Brows.Updates;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Brows.GitHub;

internal sealed class GitHubReleaseFeed {
    private HttpClient Http { get; }
    private AppVersion Current { get; }
    private string Repository { get; }

    private AppUpdate Candidate(Release release, string architecture, AppVersion pin, bool allowPreRelease) {
        if (release is null || release.Draft || !AppVersion.TryParse(release.Tag, out var version)) {
            return null;
        }
        if (!version.IsNewerThan(Current)) {
            return null;
        }
        var matches = pin is null
            ? !release.PreRelease || allowPreRelease
            : string.Equals(pin.Text, version.Text, StringComparison.OrdinalIgnoreCase);
        if (!matches) {
            return null;
        }

        return new AppUpdate {
            Version = version.Text,
            Page = release.Page,
            PreRelease = release.PreRelease,
            Published = release.Published ?? default,
            Architecture = architecture,
            Package = Package(release.Assets, architecture, version.Text)
        };
    }

    private static AppUpdatePackage Package(Asset[] assets, string architecture, string version) {
        if (architecture is null || assets is null) {
            return null;
        }

        var name = UpdatePackage.Name(architecture, version);
        foreach (var asset in assets) {
            var valid = asset is not null
                && string.Equals(asset.Name, name, StringComparison.OrdinalIgnoreCase)
                && asset.Url is { IsAbsoluteUri: true, Scheme: "https" }
                && asset.Size > 0
                && asset.Digest is { Length: 71 } digest
                && digest.StartsWith("sha256:", StringComparison.Ordinal)
                && UpdatePackage.IsSha256(digest.AsSpan(7));
            if (valid) {
                return new AppUpdatePackage {
                    Name = name,
                    Url = asset.Url,
                    Sha256 = asset.Digest[7..].ToLowerInvariant(),
                    Size = asset.Size
                };
            }
        }
        return null;
    }

    internal GitHubReleaseFeed(HttpClient http, AppVersion current, string repository) {
        Http = http ?? throw new ArgumentNullException(nameof(http));
        Current = current ?? throw new ArgumentNullException(nameof(current));
        Repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    internal async Task<AppUpdate> Find(string architecture, AppVersion pin, bool allowPreRelease,
                                        CancellationToken token) {
        if (pin is not null && !pin.IsNewerThan(Current)) {
            return null;
        }

        // One page bounds discovery to the 100 most recent releases. Pins can reach any published tag.
        var query = pin is null ? "?per_page=100" : $"/tags/v{pin.Text}";
        using (var request = new HttpRequestMessage(HttpMethod.Get,
                   $"https://api.github.com/repos/{Repository}/releases{query}")) {
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            request.Headers.UserAgent.ParseAdd($"Brows/{Current.Text}");

            using (var response = await Http.SendAsync(request, token).ConfigureAwait(false)) {
                if (pin is not null && response.StatusCode == HttpStatusCode.NotFound) {
                    return null;
                }
                response.EnsureSuccessStatusCode();
                if (pin is not null) {
                    var release = await response.Content.ReadFromJsonAsync<Release>(token).ConfigureAwait(false);
                    return Candidate(release, architecture, pin, allowPreRelease);
                }

                var releases = await response.Content.ReadFromJsonAsync<Release[]>(token).ConfigureAwait(false);
                return releases?
                    .Select(release => Candidate(release, architecture, pin, allowPreRelease))
                    .OfType<AppUpdate>()
                    .OrderByDescending(update => update.Package is not null)
                    .ThenByDescending(update => ReleaseNumber(update.Version))
                    .FirstOrDefault();
            }
        }

        static Version ReleaseNumber(string text) {
            AppVersion.TryParse(text, out var version);
            return version.Number;
        }
    }

    private sealed record Release(
        [property: JsonPropertyName("tag_name")] string Tag,
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("prerelease")] bool PreRelease,
        [property: JsonPropertyName("html_url")] Uri Page,
        [property: JsonPropertyName("published_at")] DateTimeOffset? Published,
        [property: JsonPropertyName("assets")] Asset[] Assets);

    private sealed record Asset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] Uri Url,
        [property: JsonPropertyName("size")] long Size,
        [property: JsonPropertyName("digest")] string Digest);
}
