using Domore.IO;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Conf;

internal sealed class ConfigurationFileWatcher {
    private static readonly ILog Log = Logging.For(typeof(ConfigurationFileWatcher));
    private static readonly ConfigurationFileWatcher Instance = new(Watch);

    private readonly Lock Locker = new();
    private readonly List<ConfigurationFileInfo> Subscribers = [];
    private readonly Func<Func<Task>, Func<FileSystemEventArgs, Task>, Task> WatchEvents;
    /*
     * Callers share readiness but only cancel their own wait. Keep processing
     * completion separate so a failed attempt is fully observed before retry.
     */
    private TaskCompletionSource Ready;
    private TaskCompletionSource CompletionSource;
    /*
     * The file system watcher reports events to a callback that references it
     * weakly, so the running watch is kept reachable here. Otherwise a garbage
     * collection can collect the watcher, and changes stop being reported.
     */
    private Task Running;

    private static async Task Watch(Func<Task> ready, Func<FileSystemEventArgs, Task> receive) {
        var path = await ConfigurationPath.FileReady(CancellationToken.None);
        var provider = new FileSystemEventProvider(path);
        await foreach (var e in provider.Events(ready: token => ready(), token: CancellationToken.None)) {
            await receive(e);
        }
    }

    private async Task Run(TaskCompletionSource ready, TaskCompletionSource completion) {
        try {
            await WatchEvents(() => {
                ready.TrySetResult();
                return Task.CompletedTask;
            }, Receive);
            throw new InvalidOperationException("Configuration file watching stopped.");
        }
        catch (Exception ex) {
            lock (Locker) {
                if (Ready == ready) {
                    Ready = null;
                }
                ready.TrySetException(ex);
                _ = ready.Task.Exception;
            }
            if (Log.Error()) {
                Log.Error(ex);
            }
        }
        finally {
            completion.TrySetResult();
        }
    }

    private Task Receive(FileSystemEventArgs e) {
        if (Log.Info()) {
            Log.Info($"{e?.ChangeType} > {e?.FullPath}");
        }
        List<ConfigurationFileInfo> changed;
        lock (Locker) {
            changed = Subscribers.Where(sub => {
                var name = Path.GetFileName(sub.File);

                /*
                 * The watcher reports actual case, while subscriber names are lowercased.
                 */
                return string.Equals(name, e?.Name, StringComparison.OrdinalIgnoreCase);
            }).ToList();
            foreach (var sub in changed) {
                Subscribers.Remove(sub);
            }
        }
        foreach (var sub in changed) {
            try {
                sub.Invalidate();
            }
            catch (Exception ex) {
                if (Log.Error()) {
                    Log.Error(ex);
                }
            }
        }
        return Task.CompletedTask;
    }

    internal Task Completion {
        get {
            lock (Locker) {
                return CompletionSource?.Task ?? Task.CompletedTask;
            }
        }
    }

    internal ConfigurationFileWatcher(Func<Func<Task>, Func<FileSystemEventArgs, Task>, Task> watchEvents) {
        WatchEvents = watchEvents ?? throw new ArgumentNullException(nameof(watchEvents));
    }

    internal async Task Add(ConfigurationFileInfo info, CancellationToken token) {
        token.ThrowIfCancellationRequested();
        TaskCompletionSource ready;
        TaskCompletionSource completion;
        var start = false;
        lock (Locker) {
            ready = Ready;
            if (ready is null) {
                Ready = ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
                CompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
                start = true;
            }
            completion = CompletionSource;
        }
        if (start) {
            var running = Run(ready, completion);
            lock (Locker) {
                if (CompletionSource == completion) {
                    Running = running;
                }
            }
        }
        await ready.Task.WaitAsync(token);
        lock (Locker) {
            token.ThrowIfCancellationRequested();
            if (Ready != ready) {
                throw new InvalidOperationException("Configuration file watching stopped during subscription.");
            }
            Subscribers.Add(info);
        }
    }

    public static Task Subscribe(ConfigurationFileInfo info, CancellationToken token) {
        if (Log.Info()) {
            Log.Info($"{nameof(Subscribe)} > {info?.File}");
        }
        return Instance.Add(info, token);
    }
}
