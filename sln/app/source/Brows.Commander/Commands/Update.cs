using Brows.Conf;
using Brows.Localization;
using Domore.Conf;
using Domore.Conf.Cli;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands;

internal sealed class Update : Command<Update.Parameter> {
    private static readonly ILog Log = Logging.For(typeof(Update));

    private readonly Lock State = new();

    private long Checks;
    private long NoticeSequence;
    private UpdateNotice Notice;
    private AppUpdateOutcome Outcome;

    private bool Ready => UpdateInstallable is not null && UpdateAvailable is not null && UpdateInstaller is not null;

    private string Text(string key, params object[] arguments) {
        return string.Format(CultureInfo.CurrentCulture, Translation?.Value(key) ?? key, arguments);
    }

    private async Task<UpdateConfig> LoadConfig(CancellationToken token) {
        var config = ConfigFile?.File<UpdateConfig>();
        return config is null ? new() : await config.Load(token).ConfigureAwait(false);
    }

    private async Task<(AppUpdate Update, bool Installable)> Check(AppRequest request, bool defaultRequest,
                                                                  CancellationToken token) {
        var sequence = defaultRequest ? Interlocked.Increment(ref Checks) : 0;
        var update = await UpdateAvailable.Get(request, token).ConfigureAwait(false);
        var installable = await UpdateInstallable.Get(token).ConfigureAwait(false);
        if (defaultRequest) {
            lock (State) {
                if (sequence > NoticeSequence) {
                    NoticeSequence = sequence;
                    Notice = installable && update?.Package is not null ? new(request, update) : null;
                }
            }
        }
        return (update, installable);
    }

    private async Task ReadOutcome(CancellationToken token) {
        if (UpdateOutcome is null) {
            return;
        }
        var outcome = await UpdateOutcome.Get(token).ConfigureAwait(false);
        lock (State) {
            Outcome = outcome;
        }
        if (outcome is null) {
            return;
        }
        if (outcome.Kind == AppUpdateOutcomeKind.Installed) {
            if (Log.Info()) {
                Log.Info($"Installed Brows {outcome.Version}. Installer log: {outcome.Log}");
            }
            return;
        }
        if (Log.Warn()) {
            Log.Warn(Describe(outcome));
        }
    }

    private string Describe(AppUpdateOutcome outcome) {
        return outcome.Kind switch {
            AppUpdateOutcomeKind.Failed => Text("Update_Failed", outcome.Version, outcome.ExitCode, outcome.Log),
            AppUpdateOutcomeKind.RestartRequired =>
                Text("Update_RestartRequired", outcome.Version, outcome.ExitCode, outcome.Log),
            AppUpdateOutcomeKind.NotStarted => Text("Update_NotStarted", outcome.Version),
            _ => null
        };
    }

    private async Task Background(CancellationToken token) {
        try {
            try {
                await ReadOutcome(token).ConfigureAwait(false);
            }
            catch (Exception ex) when (!token.IsCancellationRequested) {
                if (Log.Info()) {
                    Log.Info("Could not read the Brows update outcome.", ex);
                }
            }

            await Task.Delay(RemindDelay, token).ConfigureAwait(false);
            while (!token.IsCancellationRequested) {
                try {
                    var config = await LoadConfig(token).ConfigureAwait(false);
                    if (config.Check && await UpdateInstallable.Get(token).ConfigureAwait(false)) {
                        await Check(Request(config, null), defaultRequest: true, token).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (!token.IsCancellationRequested) {
                    if (Log.Info()) {
                        Log.Info("Could not check for Brows updates.", ex);
                    }
                }
                await Task.Delay(RemindInterval, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
        }
    }

    private async Task<string> GetRunningVersion(CancellationToken token) {
        return RunningVersion is null ? null : await RunningVersion.Get(token).ConfigureAwait(false);
    }

    internal IAppRunningVersion RunningVersion { get; set; }
    internal IAppUpdateInstallable UpdateInstallable { get; set; }
    internal IAppUpdateAvailable UpdateAvailable { get; set; }
    internal IAppUpdateInstaller UpdateInstaller { get; set; }
    internal IAppUpdateOutcome UpdateOutcome { get; set; }
    internal IConfigurationFile ConfigFile { get; set; }
    internal ITranslationExport Translation { get; set; }

    internal TimeSpan RemindDelay { get; init; } = TimeSpan.FromMinutes(1);
    internal TimeSpan RemindInterval { get; init; } = TimeSpan.FromHours(24);
    internal Func<ICommandContext, Action> Ending { get; init; } = context => {
        return context.HasCommander(out var commander) && commander is Commander { Domain: { } domain }
            ? domain.End
            : null;
    };

    internal static AppRequest Request(UpdateConfig config, Parameter parameter) {
        return new AppRequest {
            AllowPreRelease = parameter?.Prerelease ?? config.Prerelease,
            Architecture = Trim(parameter?.Architecture),
            Version = Trim(parameter?.Version)
        };

        static string Trim(string value) {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    protected override Task Init(CancellationToken token) {
        if (Ready) {
            _ = Task.Run(() => Background(token), token);
        }
        return Task.CompletedTask;
    }

    protected override bool Work(Context context) {
        if (!Ready || !context.GetParameter(out var parameter)) {
            return false;
        }
        var end = Ending(context);
        if (end is null) {
            return false;
        }

        return context.Operate(async (progress, token) => {
            progress.Change(name: Text("Update_Checking"));
            var config = await LoadConfig(token).ConfigureAwait(false);
            var request = Request(config, parameter);
            var (update, installable) = await Check(request, request == Request(config, null), token)
                .ConfigureAwait(false);

            if (update is null) {
                var running = await GetRunningVersion(token).ConfigureAwait(false);
                if (request.Version is not null) {
                    throw new InvalidOperationException(Text("Update_NotFound", request.Version, running));
                }
                progress.Change(data: Text("Update_Current", running));
                return true;
            }
            if (!installable) {
                throw new InvalidOperationException(Text("Update_NotInstallable", update.Version, update.Page));
            }
            if (update.Package is null) {
                throw new InvalidOperationException(
                    Text("Update_NoInstaller", update.Version, update.Page, update.Architecture));
            }

            progress.Change(name: Text("Update_Downloading", update.Version, update.Architecture));
            if (!await UpdateInstaller.Work(update, progress, token).ConfigureAwait(false)) {
                throw new InvalidOperationException(Text("Update_InProgress"));
            }
            end();
            return true;
        });
    }

    protected override async IAsyncEnumerable<ICommandSuggestion> Suggest(
        Context context, [EnumeratorCancellation] CancellationToken token) {
        await foreach (var suggestion in base.Suggest(context, token).ConfigureAwait(false)) {
            yield return suggestion;
        }

        var empty = context.HasLine(out var line) && !line.HasCommand(out _);
        var input = InputTrigger;
        if (!empty || string.IsNullOrWhiteSpace(input)) {
            yield break;
        }

        AppUpdateOutcome outcome;
        UpdateNotice notice;
        lock (State) {
            (outcome, notice) = (Outcome, Notice);
        }

        if (outcome is not null && Describe(outcome) is { } description) {
            yield return Suggestion(context, input, group: "Update", groupOrder: -1, description: description);
        }
        var config = ConfigFile?.File<UpdateConfig>().Loaded ?? new();
        if (notice is not null && notice.Request == Request(config, null)) {
            var version = notice.Update.Version;
            yield return Suggestion(context, $"{input} {version}", group: "Update", groupOrder: -1,
                description: Text("Update_Available", version));
        }
    }

    private sealed record UpdateNotice(AppRequest Request, AppUpdate Update);

    public sealed record Parameter {
        [CliArgument]
        public string Version { get; init; }

        [Conf("arch")]
        public string Architecture { get; init; }

        public bool? Prerelease { get; init; }
    }
}
