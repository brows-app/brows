using Brows.Git;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RuntimeOperatingSystem = System.OperatingSystem;

namespace Brows;

internal sealed class GitStatusProvider : Provider<GitStatusEntry, GitStatusConfig> {
    private readonly SemaphoreSlim ReloadLock = new(1);
    private CancellationTokenSource MonitorSource;
    private bool Begun;
    private bool Ended;

    private async Task Apply(GitStatusView status, CancellationToken token) {
        /*
         * Git may report separate index and working-tree entries for the same path.
         */
        var rows = new Dictionary<string, GitStatusRow>(StringComparer.Ordinal);
        foreach (var row in status.Files) {
            if (rows.TryGetValue(row.Path, out var previous)) {
                rows[row.Path] = new(
                    row.Path, previous.OriginalPath ?? row.OriginalPath,
                    previous.Status | row.Status);
            }
            else {
                rows.Add(row.Path, row);
            }
        }
        if (!ShowIgnored) {
            var paths = rows
                .Where(pair => pair.Value.Status == GitStatusFlags.Ignored)
                .Select(pair => pair.Key)
                .ToArray();
            foreach (var path in paths) {
                rows.Remove(path);
            }
        }
        await Revoke(Provided.Where(entry => !rows.ContainsKey(entry.ID)).ToArray(), token);
        var added = new List<GitStatusEntry>();
        var updated = false;
        foreach (var row in rows.Values) {
            if (token.IsCancellationRequested) {
                token.ThrowIfCancellationRequested();
            }
            var entry = Lookup(id: row.Path);
            if (entry is null) {
                added.Add(new(this, row));
            }
            else {
                updated |= await entry.Update(row, token);
            }
        }
        await Provide(added, token);
        if (updated) {
            ((IEntryProvider)this).Observation.DataView.Sort(Sorting);
        }
        Status = status;
        Error = null;
    }

    private bool SameRepository(GitStatusView status) => string.Equals(
        Path.TrimEndingDirectorySeparator(status.GitDirectory),
        Path.TrimEndingDirectorySeparator(Status.GitDirectory),
        RuntimeOperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private async Task ReloadSafely(CancellationToken token) {
        try {
            await Reload(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
        }
        catch (Exception) {
            /*
             * Reload exposes the failure in Error; a later refresh can recover.
             */
        }
    }

    private async Task Monitor(CancellationTokenSource source) {
        var token = source.Token;
        try {
            for (; ; ) {
                if (token.IsCancellationRequested) {
                    break;
                }
                await Task.Delay(TimeSpan.FromSeconds(2), token);
                await ReloadSafely(token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
        }
    }

    private void Stop() {
        Ended = true;
        var monitorSource = MonitorSource;
        MonitorSource = null;
        monitorSource?.Cancel();
        monitorSource?.Dispose();
    }

    internal Task Monitoring { get; private set; } = Task.CompletedTask;

    internal async Task Reload(CancellationToken token) {
        await ReloadLock.WaitAsync(token);
        try {
            token.ThrowIfCancellationRequested();
            if (Ended) {
                return;
            }
            IsRefreshing = true;
            GitStatusView status = await GitStatusReader.Read(RepositoryPath, token, refresh: true);
            if (status == null || !SameRepository(status)) {
                throw new IOException($"The Git repository is no longer available: {RepositoryPath}");
            }
            token.ThrowIfCancellationRequested();
            if (!Ended) {
                await Apply(status, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
            throw;
        }
        catch (Exception error) {
            Error = error.Message;
            throw;
        }
        finally {
            IsRefreshing = false;
            ReloadLock.Release();
        }
    }

    protected sealed override async Task Begin(CancellationToken token) {
        if (Begun || Ended) {
            return;
        }
        await ReloadLock.WaitAsync(token);
        try {
            if (!Begun && !Ended) {
                Begun = true;
                try {
                    await Apply(Status, token);
                }
                finally {
                    if (!Ended && !token.IsCancellationRequested) {
                        MonitorSource = CancellationTokenSource.CreateLinkedTokenSource(token);
                        Monitoring = Monitor(MonitorSource);
                    }
                }
            }
        }
        finally {
            ReloadLock.Release();
        }
    }

    protected sealed override Task Refresh(CancellationToken token) => Reload(token);

    protected sealed override void End() => Stop();

    protected sealed override void Dispose(bool disposing) {
        if (disposing) {
            Stop();
        }
        base.Dispose(disposing);
    }

    public string RepositoryPath { get; }
    public sealed override string Parent => RepositoryPath;

    public GitStatusView Status {
        get;
        private set => Change(ref field, value);
    }

    public string Error {
        get;
        private set => Change(ref field, value);
    }

    public bool IsRefreshing {
        get;
        private set => Change(ref field, value);
    }

    public bool ShowIgnored {
        get;
        set {
            if (Change(ref field, value) && Begun && !Ended) {
                _ = ReloadSafely(Token);
            }
        }
    }

    public GitStatusProvider(string repositoryPath, GitStatusView status)
    : base(GitStatusProviderFactory.IDFor(repositoryPath)) {
        RepositoryPath = repositoryPath ?? throw new ArgumentNullException(nameof(repositoryPath));
        Status = status ?? throw new ArgumentNullException(nameof(status));
    }
}
