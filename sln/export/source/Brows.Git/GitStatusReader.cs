using Brows.Exports;
using Brows.Git;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal static class GitStatusReader {
    private sealed class Cached {
        public DateTime Expires { get; } = DateTime.UtcNow.AddSeconds(2);
        public Task<GitStatusView> Task { get; }

        public Cached(GitRepo repo) {
            Task = ReadCore(repo);
        }
    }

    private static readonly Dictionary<string, Cached> Cache = new(StringComparer.Ordinal);
    private static readonly AsyncLocal<bool> RefreshingEntries = new();

    private static async Task<GitStatusView> ReadCore(GitRepo repo) {
        using (repo) {
            return new GitStatusView(await repo.Status(CancellationToken.None).ConfigureAwait(false));
        }
    }

    internal static void Invalidate() {
        if (RefreshingEntries.Value) {
            return;
        }
        lock (Cache) {
            Cache.Clear();
        }
    }

    public static async Task RefreshEntries(IEntryProvider provider, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(provider);
        var columns = GitFileSystemData.Columns
            .Where(column => provider.Observation.DataView.Has(column.Key))
            .ToArray();
        if (columns.Length == 0) {
            return;
        }
        var directories = new Dictionary<string, GitStatusView>(StringComparer.Ordinal);
        var previous = RefreshingEntries.Value;
        RefreshingEntries.Value = true;
        try {
            foreach (var entry in provider.Observation.Observed.ToArray()) {
                if (entry is not IFileSystemEntry file) {
                    continue;
                }
                var directory = file.Kind == FileSystemEntryKind.Directory
                    ? file.Path
                    : Path.GetDirectoryName(file.Path);
                if (directory is null) {
                    continue;
                }
                if (!directories.TryGetValue(directory, out var view)) {
                    try {
                        directories[directory] = view = await Read(directory, token);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) {
                        throw;
                    }
                    catch (Exception) {
                        /*
                         * The entry data reports its own error; 
                         * a child repository must not hide the parent status.
                         */
                        foreach (var column in columns) {
                            await entry[column.Key].Refresh(token);
                        }
                        continue;
                    }
                }
                foreach (var column in columns) {
                    if (token.IsCancellationRequested) {
                        token.ThrowIfCancellationRequested();
                    }
                    var data = entry[column.Key];
                    if (!Equals(data.Value, column.Display(view, file))) {
                        await data.Refresh(token);
                    }
                }
            }
        }
        finally {
            RefreshingEntries.Value = previous;
        }
    }

    public static async Task<GitStatusView> Read(string directory, CancellationToken token, bool refresh = false) {
        ArgumentNullException.ThrowIfNull(directory);
        if (token.IsCancellationRequested){
            token.ThrowIfCancellationRequested();
        }
        var repo = await GitRepo.Find(directory, search: true, token).ConfigureAwait(false);
        if (repo is null) {
            return null;
        }
        Task<GitStatusView> task;
        try {
            var key = repo.Path;
            lock (Cache) {
                Cache.TryGetValue(key, out var cached);
                if (refresh || cached is null ||
                    (cached.Task.IsCompleted && (cached.Expires <= DateTime.UtcNow || !cached.Task.IsCompletedSuccessfully))) {
                    if (Cache.Count >= 128) {
                        Cache.Clear();
                    }
                    Cache[key] = cached = new(repo);
                    repo = null;
                }
                task = cached.Task;
            }
        }
        finally {
            repo?.Dispose();
        }
        return await task.WaitAsync(token).ConfigureAwait(false);
    }
}
