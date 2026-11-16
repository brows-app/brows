using Brows.Conf.ConfigurationFileInfos;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Conf;

[TestFixture]
internal sealed class ConfigurationFileWatcherTest {
    private static ConfigurationFileInfo Info(string name = "watcher.conf") {
        return new TypedConfigurationFileInfo<object>(name, "default.conf", "brows");
    }

    [Test]
    public async Task Add_BeforeReady_WaitsAndRegistersBeforeReturning() {
        var events = new WatchEvents();
        var watcher = new ConfigurationFileWatcher(events.Start);
        var info = Info();
        var subscription = watcher.Add(info, CancellationToken.None);

        Assert.That(subscription.IsCompleted, Is.False);
        Assert.That(events.Starts, Is.EqualTo(1));
        await events.Ready();
        await subscription.WaitAsync(TimeSpan.FromSeconds(10));
        await events.Receive(new FileSystemEventArgs(WatcherChangeTypes.Changed, ".", "other.conf"));
        Assert.That(info.Invalid, Is.False);
        await events.Receive(new FileSystemEventArgs(WatcherChangeTypes.Changed, ".", "WATCHER.conf"));
        Assert.That(info.Invalid, Is.True);
    }

    [Test]
    public async Task Add_AlreadyCanceled_DoesNotStartWatcher() {
        var events = new WatchEvents();
        var watcher = new ConfigurationFileWatcher(events.Start);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await watcher.Add(Info(), cancellation.Token));
        Assert.That(events.Starts, Is.Zero);
    }

    [Test]
    public async Task Add_CanceledWaiter_DoesNotAbandonSharedStartup() {
        var events = new WatchEvents();
        var watcher = new ConfigurationFileWatcher(events.Start);
        using var cancellation = new CancellationTokenSource();
        var canceledInfo = Info();
        var first = watcher.Add(canceledInfo, cancellation.Token);
        var info = Info();
        var second = watcher.Add(info, CancellationToken.None);

        cancellation.Cancel();
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await first);
        Assert.That(second.IsCompleted, Is.False);
        await events.Ready();
        await second.WaitAsync(TimeSpan.FromSeconds(10));
        var later = Info();
        await watcher.Add(later, CancellationToken.None);
        await events.Receive(new FileSystemEventArgs(WatcherChangeTypes.Changed, ".", "watcher.conf"));

        Assert.That(events.Starts, Is.EqualTo(1));
        Assert.That(canceledInfo.Invalid, Is.False);
        Assert.That(info.Invalid, Is.True);
        Assert.That(later.Invalid, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Add_StartupFails_ReportsFailureAndAllowsRetry(bool synchronous) {
        var events = new WatchEvents();
        var failure = new IOException("startup failure");
        var startup = new TaskCompletionSource();
        var attempts = 0;
        var watcher = new ConfigurationFileWatcher((ready, receive) => {
            attempts++;
            if (attempts == 1) {
                if (synchronous) {
                    throw failure;
                }
                return startup.Task;
            }
            return events.Start(ready, receive);
        });
        var info = Info();

        var first = watcher.Add(info, CancellationToken.None);
        if (synchronous == false) {
            Assert.That(first.IsCompleted, Is.False);
            startup.SetException(failure);
        }
        var actual = await Assert.ThrowsAsync<IOException>(async () => await first);
        Assert.That(actual, Is.SameAs(failure));
        Assert.That(info.Invalid, Is.False);
        var retry = watcher.Add(info, CancellationToken.None);
        Assert.That(retry.IsCompleted, Is.False);
        await events.Ready();
        await retry.WaitAsync(TimeSpan.FromSeconds(10));
        await events.Receive(new FileSystemEventArgs(WatcherChangeTypes.Changed, ".", "watcher.conf"));
        Assert.That(info.Invalid, Is.True);
        Assert.That(attempts, Is.EqualTo(2));
    }

    [Test]
    public async Task Add_WatcherFailsAfterReady_LaterSubscriptionRestartsWatcher() {
        var events = new WatchEvents();
        var watcher = new ConfigurationFileWatcher(events.Start);
        var first = Info();
        var previousContext = SynchronizationContext.Current;
        Task subscription;
        SynchronizationContext.SetSynchronizationContext(new WatchContext());
        try {
            subscription = watcher.Add(first, CancellationToken.None);
        }
        finally {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
        await events.Ready();
        await subscription.WaitAsync(TimeSpan.FromSeconds(10));

        var completion = watcher.Completion;
        events.Lifetime.SetException(new IOException("watcher failure"));
        await completion.WaitAsync(TimeSpan.FromSeconds(10));
        var later = Info();
        var restart = watcher.Add(later, CancellationToken.None);
        Assert.That(events.Starts, Is.EqualTo(2));
        Assert.That(restart.IsCompleted, Is.False);
        await events.Ready();
        await restart.WaitAsync(TimeSpan.FromSeconds(10));
        await events.Receive(new FileSystemEventArgs(WatcherChangeTypes.Changed, ".", "watcher.conf"));
        Assert.That(first.Invalid, Is.True);
        Assert.That(later.Invalid, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Add_WatcherStopsDuringStartup_FailsWithoutAutomaticRetry(bool faulted) {
        var attempts = 0;
        var watcher = new ConfigurationFileWatcher((ready, receive) => {
            attempts++;
            ready().GetAwaiter().GetResult();
            return faulted ? Task.FromException(new IOException("watcher failure")) : Task.CompletedTask;
        });

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await watcher.Add(Info(), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.That(attempts, Is.EqualTo(1));
    }

    [Test]
    public async Task Subscribe_ChangedWithDifferentCase_RaisesChanged() {
        var dir = await ConfigurationPath.FileReady(CancellationToken.None);
        var name = nameof(WatcherCase5904Conf);
        var upper = Path.Combine(dir, $"{name}.conf");
        var ownsFile = false;
        IConfiguration<WatcherCase5904Conf> manager = new ConfigurationFileManager<WatcherCase5904Conf>();
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler handler = (sender, e) => changed.TrySetResult();
        manager.Changed += handler;
        try {
            await manager.Load(CancellationToken.None);

            /*
             * Load must confirm watcher readiness; the immediate edit differs from
             * the lowercased subscription name in case alone.
             */
            using (var owned = new FileStream(upper, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                ownsFile = true;
            }
            await File.WriteAllTextAsync(upper, "value = 1");
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var reloaded = await manager.Load(CancellationToken.None);
            Assert.That(reloaded.Value, Is.EqualTo(1));
        }
        finally {
            manager.Changed -= handler;
            if (ownsFile) {
                File.Delete(upper);
            }
        }
    }

    [Test]
    public async Task Subscribe_ChangedAfterGarbageCollection_RaisesChanged() {
        var dir = await ConfigurationPath.FileReady(CancellationToken.None);
        var name = nameof(WatcherCollected7321Conf);
        var path = Path.Combine(dir, $"{name}.conf");
        var ownsFile = false;
        IConfiguration<WatcherCollected7321Conf> manager = new ConfigurationFileManager<WatcherCollected7321Conf>();
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler handler = (sender, e) => changed.TrySetResult();
        manager.Changed += handler;
        try {
            await manager.Load(CancellationToken.None);

            /*
             * Nothing but the running watch references the file system watcher,
             * so a full collection must not stop changes from being reported.
             */
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            using (var owned = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                ownsFile = true;
            }
            await File.WriteAllTextAsync(path, "value = 1");
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var reloaded = await manager.Load(CancellationToken.None);
            Assert.That(reloaded.Value, Is.EqualTo(1));
        }
        finally {
            manager.Changed -= handler;
            if (ownsFile) {
                File.Delete(path);
            }
        }
    }

    private sealed class WatchContext : SynchronizationContext {
        public sealed override void Post(SendOrPostCallback callback, object state) {
            ThreadPool.QueueUserWorkItem(_ => callback(state));
        }
    }

    private sealed class WatchEvents {
        public int Starts { get; private set; }
        public Func<Task> Ready { get; private set; }
        public Func<FileSystemEventArgs, Task> Receive { get; private set; }
        public TaskCompletionSource Lifetime { get; private set; }

        public Task Start(Func<Task> ready, Func<FileSystemEventArgs, Task> receive) {
            Starts++;
            Ready = ready;
            Receive = receive;
            Lifetime = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return Lifetime.Task;
        }
    }

    private sealed class WatcherCase5904Conf {
        public int Value { get; set; }
    }

    private sealed class WatcherCollected7321Conf {
        public int Value { get; set; }
    }
}
