using Brows.GitHub;
using Brows.Updates;
using System.Net.Http;
using System.Threading.Tasks;

namespace Brows;

internal sealed class AppUpdateFeed : IAppUpdateAvailable {
    internal UpdateInstallation Installation { get => field ??= UpdateInstallation.Current; init; }
    internal HttpClient Http { get => field ??= UpdateHttp.Client; init; }
    internal string Repository { get => field ??= UpdateRepository.Current; init; }

    Task<AppUpdate> IAppUpdateAvailable.Get(AppRequest request, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(request);
        var current = Installation.Version
            ?? throw new InvalidOperationException("The running Brows version could not be read.");

        var architecture = Installation.Architecture;
        if (request.Architecture is { } requested) {
            var supported = AppArchitecture.TryNormalize(requested, out architecture)
                && AppArchitecture.Runs(architecture, Installation.OSArchitecture);
            if (!supported) {
                throw new ArgumentException($"Installer architecture '{requested}' cannot run on this Windows.",
                    nameof(request.Architecture));
            }
        }

        AppVersion pin = null;
        if (request.Version is { } version && !AppVersion.TryParse(version, out pin)) {
            throw new ArgumentException($"'{version}' is not a valid Brows version.", nameof(request.Version));
        }

        return new GitHubReleaseFeed(Http, current, Repository).Find(architecture, pin, request.AllowPreRelease,
            token);
    }
}
