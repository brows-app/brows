using Brows.Composition;
using Brows.Operations;
using Brows.Updates;
using Domore.Logs;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed class AppUpdateInstaller : IAppUpdateInstaller, IExportAndInit {
    private const string HelperFile = "brows-update.exe";
    private static readonly ILog Log = Logging.For(typeof(AppUpdateInstaller));

    private int Installing;

    private string HandoffDirectory => Path.Combine(Installation.UpdateDirectory, "handoff");

    private static bool Start(ProcessStartInfo info) {
        using (var process = Process.Start(info)) {
            return process is not null;
        }
    }

    private string Validate(AppUpdate update) {
        if (!Installation.Installable) {
            throw new InvalidOperationException("Only Brows copies in the per-user install directory can be updated.");
        }

        ArgumentNullException.ThrowIfNull(update);
        var package = update.Package
            ?? throw new ArgumentException("An update must include an installer.", nameof(update.Package));
        var versionValid = AppVersion.TryParse(update.Version, out var version)
            && version.Text == update.Version && version.IsNewerThan(Installation.Version);
        if (!versionValid) {
            throw new ArgumentException("An update must specify a newer Brows version.", nameof(update.Version));
        }

        var architectureValid = AppArchitecture.TryNormalize(update.Architecture, out var architecture)
            && architecture == update.Architecture && AppArchitecture.Runs(architecture, Installation.OSArchitecture);
        if (!architectureValid) {
            throw new ArgumentException("The installer architecture is invalid for this Windows.",
                nameof(update.Architecture));
        }

        var name = UpdatePackage.Name(architecture, version.Text);
        if (package.Name != name) {
            throw new ArgumentException("The installer filename does not match the update.", nameof(package.Name));
        }

        var url = $"https://github.com/{Repository}/releases/download/v{version.Text}/{name}";
        var urlValid = package.Url is { IsAbsoluteUri: true }
            && string.Equals(package.Url.OriginalString, url, StringComparison.OrdinalIgnoreCase);
        if (!urlValid) {
            throw new ArgumentException("The installer URL does not match the GitHub release asset.",
                nameof(package.Url));
        }

        if (!UpdatePackage.IsSha256(package.Sha256.AsSpan())) {
            throw new ArgumentException("The installer must have a SHA-256 digest.", nameof(package.Sha256));
        }
        if (package.Size is <= 0 or > UpdatePackage.MaximumSize) {
            throw new ArgumentException("The installer size is outside the allowed range.", nameof(package.Size));
        }

        var directory = Path.GetFullPath(Path.Combine(Installation.UpdateDirectory, version.Text));
        if (!string.Equals(Path.GetDirectoryName(directory), Installation.UpdateDirectory,
                StringComparison.OrdinalIgnoreCase)) {
            throw new ArgumentException("The update version must name a folder in the update cache.",
                nameof(update.Version));
        }
        return directory;
    }

    private ProcessStartInfo PrepareHelper(string msi, string directory, string version, CancellationToken token) {
        Directory.CreateDirectory(HandoffDirectory);
        token.ThrowIfCancellationRequested();
        var helper = Path.Combine(HandoffDirectory, HelperFile);
        File.Copy(Path.Combine(Installation.ProgramDirectory, HelperFile), helper, overwrite: true);

        return new ProcessStartInfo(helper) {
            UseShellExecute = false,
            WorkingDirectory = HandoffDirectory,
            Arguments = string.Join(' ', [
                $"pid={Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}",
                $"msi=\"{msi}\"",
                $"log=\"{Path.Combine(directory, "install.log")}\"",
                $"outcome=\"{Path.Combine(Installation.UpdateDirectory, "outcome.json")}\"",
                $"version={version}",
                $"updates=\"{Installation.UpdateDirectory}\"",
                $"launch=\"{Path.Combine(Installation.AppDirectory, "brows.exe")}\""
            ])
        };
    }

    internal UpdateInstallation Installation { get => field ??= UpdateInstallation.Current; init; }
    internal HttpClient Http { get => field ??= UpdateHttp.Client; init; }
    internal string Repository { get => field ??= UpdateRepository.Current; init; }
    internal Func<ProcessStartInfo, bool> StartHelper { get; init; } = Start;

    Task IExportAndInit.Init(IExportContext context, CancellationToken token) {
        return Task.Run(() => {
            try {
                var canClean = Installation.Installable && !UpdateHandoff.Running()
                    && Directory.Exists(HandoffDirectory);
                if (canClean) {
                    Directory.Delete(HandoffDirectory, recursive: true);
                }
            }
            catch (Exception ex) {
                if (Log.Info()) {
                    Log.Info("Could not clean the Brows update handoff directory.", ex);
                }
            }
        }, token);
    }

    async Task<bool> IAppUpdateInstaller.Work(AppUpdate update, IOperationProgress progress, CancellationToken token) {
        var directory = Validate(update);
        if (Interlocked.CompareExchange(ref Installing, 1, 0) != 0) {
            return false;
        }

        var started = false;
        try {
            if (UpdateHandoff.Running()) {
                return false;
            }

            var msi = await new UpdateDownload(Http).File(update, directory, progress, token).ConfigureAwait(false);
            var info = PrepareHelper(msi, directory, update.Version, token);
            token.ThrowIfCancellationRequested();
            started = StartHelper(info);
            if (!started) {
                throw new InvalidOperationException("The Brows update helper could not be started.");
            }
            return true;
        }
        finally {
            if (!started) {
                Interlocked.Exchange(ref Installing, 0);
            }
        }
    }
}
