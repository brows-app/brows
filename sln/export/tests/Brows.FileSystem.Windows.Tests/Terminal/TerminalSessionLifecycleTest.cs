using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Terminal;

[TestFixture]
internal sealed class TerminalSessionLifecycleTest {
    [Test]
    public async Task OpenAsync_ConcurrentCallersShareOneStart() {
        var adapter = new FakeTerminalSessionAdapter { DelayStart = true };
        var lifecycle = new TerminalSessionLifecycle(adapter);
        var firstOpen = lifecycle.OpenAsync(CancellationToken.None);
        await adapter.StartEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var secondOpen = lifecycle.OpenAsync(CancellationToken.None);

        Assert.That(adapter.StartCount, Is.EqualTo(1));
        Assert.That(lifecycle.State, Is.EqualTo(TerminalSessionState.Starting));
        adapter.StartGate.TrySetResult();
        await Task.WhenAll(firstOpen, secondOpen).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(lifecycle.State, Is.EqualTo(TerminalSessionState.Running));
    }

    [Test]
    public async Task CloseAsync_DuringStartStopsLateSessionWithoutPublishingRunning() {
        var adapter = new FakeTerminalSessionAdapter { DelayStart = true };
        var lifecycle = new TerminalSessionLifecycle(adapter);
        var states = new List<TerminalSessionState>();
        lifecycle.Changed += (_, args) => states.Add(args.State);
        var open = lifecycle.OpenAsync(CancellationToken.None);
        await adapter.StartEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var close = lifecycle.CloseAsync(CancellationToken.None);
        Assert.That(lifecycle.State, Is.EqualTo(TerminalSessionState.Stopping));
        adapter.StartGate.TrySetResult();
        await Task.WhenAll(open, close).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(lifecycle.State, Is.EqualTo(TerminalSessionState.Stopped));
        Assert.That(adapter.StopCount, Is.EqualTo(1));
        Assert.That(states, Does.Not.Contain(TerminalSessionState.Running));
    }

    [Test]
    public async Task OpenAsync_CancelledCallerDoesNotCancelAnotherOpen() {
        var adapter = new FakeTerminalSessionAdapter { DelayStart = true };
        var lifecycle = new TerminalSessionLifecycle(adapter);
        using var cancellation = new CancellationTokenSource();
        var cancelledOpen = lifecycle.OpenAsync(cancellation.Token);
        await adapter.StartEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var validOpen = lifecycle.OpenAsync(CancellationToken.None);
        cancellation.Cancel();

        await Assert.CatchAsync<OperationCanceledException>(async () => await cancelledOpen);
        adapter.StartGate.TrySetResult();
        await validOpen.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(adapter.StartCount, Is.EqualTo(1));
        Assert.That(adapter.StopCount, Is.Zero);
        Assert.That(lifecycle.State, Is.EqualTo(TerminalSessionState.Running));
    }

    [Test]
    public async Task OpenAsync_AcceptedRunningRequestPreventsPendingPeerCleanup() {
        var adapter = new FakeTerminalSessionAdapter { DelayStart = true };
        var lifecycle = new TerminalSessionLifecycle(adapter);
        using var cancellation = new CancellationTokenSource();
        var acceptedOpen = false;
        lifecycle.Changed += (_, args) => {
            if (args.State == TerminalSessionState.Running) {
                lifecycle.OpenAsync(CancellationToken.None).GetAwaiter().GetResult();
                acceptedOpen = true;
                cancellation.Cancel();
            }
        };
        var pendingOpen = lifecycle.OpenAsync(cancellation.Token);
        await adapter.StartEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        adapter.StartGate.TrySetResult();

        await Assert.CatchAsync<OperationCanceledException>(async () => await pendingOpen);

        Assert.That(acceptedOpen, Is.True);
        Assert.That(adapter.StopCount, Is.Zero);
        Assert.That(lifecycle.State, Is.EqualTo(TerminalSessionState.Running));
        await lifecycle.CloseAsync(CancellationToken.None);
    }

    [Test]
    public async Task OpenAsync_SoleCancelledCallerStopsLateSession() {
        var adapter = new FakeTerminalSessionAdapter { DelayStart = true };
        var lifecycle = new TerminalSessionLifecycle(adapter);
        using var cancellation = new CancellationTokenSource();
        var open = lifecycle.OpenAsync(cancellation.Token);
        await adapter.StartEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        await Assert.CatchAsync<OperationCanceledException>(async () => await open);
        adapter.StartGate.TrySetResult();
        await adapter.StopCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await lifecycle.CloseAsync(CancellationToken.None);

        Assert.That(adapter.StopCount, Is.EqualTo(1));
        Assert.That(lifecycle.State, Is.EqualTo(TerminalSessionState.Stopped));
    }

    [Test]
    public async Task CloseAsync_CoalescesStopAndLaterCloseRetriesFailure() {
        var adapter = new FakeTerminalSessionAdapter();
        var lifecycle = new TerminalSessionLifecycle(adapter);
        await lifecycle.OpenAsync(CancellationToken.None);
        adapter.StopWork = () => Task.FromException(new InvalidOperationException("stop failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await lifecycle.CloseAsync(CancellationToken.None));
        Assert.That(lifecycle.State, Is.EqualTo(TerminalSessionState.Failed));
        adapter.StopWork = null;
        await lifecycle.CloseAsync(CancellationToken.None);

        Assert.That(adapter.StopCount, Is.EqualTo(2));
        Assert.That(lifecycle.State, Is.EqualTo(TerminalSessionState.Stopped));
    }

    [Test]
    public async Task OpenAsync_StartExceptionAndFailureEventPublishOneFailure() {
        var adapter = new FakeTerminalSessionAdapter();
        var lifecycle = new TerminalSessionLifecycle(adapter);
        var changes = new List<TerminalSessionLifecycleEventArgs>();
        lifecycle.Changed += (_, args) => changes.Add(args);
        var failure = new InvalidOperationException("launch failed");
        adapter.StartWork = () => {
            lifecycle.ProcessFailed(failure);
            return Task.FromException(failure);
        };

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await lifecycle.OpenAsync(CancellationToken.None));

        Assert.That(lifecycle.State, Is.EqualTo(TerminalSessionState.Failed));
        Assert.That(changes.FindAll(args => args.State == TerminalSessionState.Failed), Has.Count.EqualTo(1));
        Assert.That(changes.Find(args => args.State == TerminalSessionState.Failed).Exception, Is.SameAs(failure));
    }

    [TestCase(0x80000000u)]
    [TestCase(uint.MaxValue)]
    public async Task ProcessExited_PreservesUnsignedExitCode(uint exitCode) {
        var adapter = new FakeTerminalSessionAdapter();
        var lifecycle = new TerminalSessionLifecycle(adapter);
        TerminalSessionLifecycleEventArgs exit = null;
        lifecycle.Changed += (_, args) => exit = args;
        await lifecycle.OpenAsync(CancellationToken.None);
        lifecycle.ProcessExited(exitCode);

        Assert.That(exit.ExitCode, Is.EqualTo(exitCode));
    }

    [Test]
    public async Task ProcessExited_RetainsExitUntilExplicitOpenStartsAgain() {
        var adapter = new FakeTerminalSessionAdapter();
        var lifecycle = new TerminalSessionLifecycle(adapter);
        var changes = new List<TerminalSessionLifecycleEventArgs>();
        lifecycle.Changed += (_, args) => changes.Add(args);
        await lifecycle.OpenAsync(CancellationToken.None);
        lifecycle.ProcessExited(23);

        Assert.That(lifecycle.State, Is.EqualTo(TerminalSessionState.Exited));
        Assert.That(adapter.StartCount, Is.EqualTo(1));
        Assert.That(changes[^1].ExitCode, Is.EqualTo(23));
        await lifecycle.OpenAsync(CancellationToken.None);

        Assert.That(adapter.StartCount, Is.EqualTo(2));
        Assert.That(lifecycle.State, Is.EqualTo(TerminalSessionState.Running));
    }

    [Test]
    public async Task ProcessEvents_AfterCloseDoNotChangeStoppedState() {
        var adapter = new FakeTerminalSessionAdapter();
        var lifecycle = new TerminalSessionLifecycle(adapter);
        await lifecycle.OpenAsync(CancellationToken.None);
        await lifecycle.CloseAsync(CancellationToken.None);

        lifecycle.ProcessExited(4);
        lifecycle.ProcessFailed(new InvalidOperationException("late event"));

        Assert.That(lifecycle.State, Is.EqualTo(TerminalSessionState.Stopped));
    }

    [Test]
    public async Task CloseAsync_ConcurrentCallersShareOneStop() {
        var adapter = new FakeTerminalSessionAdapter();
        var stopGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        adapter.StopWork = () => stopGate.Task;
        var lifecycle = new TerminalSessionLifecycle(adapter);
        await lifecycle.OpenAsync(CancellationToken.None);

        var firstClose = lifecycle.CloseAsync(CancellationToken.None);
        await adapter.StopEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var secondClose = lifecycle.CloseAsync(CancellationToken.None);
        Assert.That(adapter.StopCount, Is.EqualTo(1));
        stopGate.TrySetResult();
        await Task.WhenAll(firstClose, secondClose).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(adapter.StopCount, Is.EqualTo(1));
        Assert.That(lifecycle.State, Is.EqualTo(TerminalSessionState.Stopped));
    }

    [Test]
    public async Task CloseAsync_CallerCancellationDoesNotCancelCleanup() {
        var adapter = new FakeTerminalSessionAdapter();
        var stopGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        adapter.StopWork = () => stopGate.Task;
        var lifecycle = new TerminalSessionLifecycle(adapter);
        await lifecycle.OpenAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var close = lifecycle.CloseAsync(cancellation.Token);
        await adapter.StopEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        await Assert.CatchAsync<OperationCanceledException>(async () => await close);
        stopGate.TrySetResult();
        await adapter.StopCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await lifecycle.CloseAsync(CancellationToken.None);

        Assert.That(lifecycle.State, Is.EqualTo(TerminalSessionState.Stopped));
    }

    [Test]
    public async Task CloseAsync_StartAndStopFailuresAreReportedAsSeparateOperations() {
        var adapter = new FakeTerminalSessionAdapter { DelayStart = true };
        var lifecycle = new TerminalSessionLifecycle(adapter);
        var changes = new List<TerminalSessionLifecycleEventArgs>();
        lifecycle.Changed += (_, args) => changes.Add(args);
        var startFailure = new InvalidOperationException("launch failed");
        var stopFailure = new IOException("cleanup failed");
        adapter.StartWork = () => Task.FromException(startFailure);
        adapter.StopWork = () => Task.FromException(stopFailure);
        var open = lifecycle.OpenAsync(CancellationToken.None);
        await adapter.StartEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var close = lifecycle.CloseAsync(CancellationToken.None);
        adapter.StartGate.TrySetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await open);
        await Assert.ThrowsAsync<IOException>(async () => await close);
        var failures = changes.FindAll(args => args.State == TerminalSessionState.Failed);
        Assert.That(failures, Has.Count.EqualTo(2));
        Assert.That(failures[0].OperationId, Is.Not.EqualTo(failures[1].OperationId));
        Assert.That(failures[0].Exception, Is.SameAs(startFailure));
        Assert.That(failures[1].Exception, Is.SameAs(stopFailure));
        Assert.That(lifecycle.State, Is.EqualTo(TerminalSessionState.Failed));

        adapter.StopWork = null;
        await lifecycle.CloseAsync(CancellationToken.None);
        Assert.That(lifecycle.State, Is.EqualTo(TerminalSessionState.Stopped));
    }

    [TestCase(typeof(DirectoryNotFoundException))]
    [TestCase(typeof(UnauthorizedAccessException))]
    [TestCase(typeof(IOException))]
    [TestCase(typeof(Win32Exception))]
    public async Task OpenAsync_LaunchFailureIsReportedOnceAndExplicitRetryCleansUp(Type exceptionType) {
        var adapter = new FakeTerminalSessionAdapter();
        var lifecycle = new TerminalSessionLifecycle(adapter);
        var changes = new List<TerminalSessionLifecycleEventArgs>();
        lifecycle.Changed += (_, args) => changes.Add(args);
        var failure = (Exception)Activator.CreateInstance(exceptionType);
        adapter.StartWork = () => Task.FromException(failure);

        await Assert.ThrowsAsync(exceptionType, async () =>
            await lifecycle.OpenAsync(CancellationToken.None));
        Assert.That(lifecycle.State, Is.EqualTo(TerminalSessionState.Failed));
        Assert.That(changes.FindAll(args => args.State == TerminalSessionState.Failed), Has.Count.EqualTo(1));
        adapter.StartWork = null;
        await lifecycle.OpenAsync(CancellationToken.None);

        Assert.That(adapter.StartCount, Is.EqualTo(2));
        Assert.That(adapter.StopCount, Is.EqualTo(1));
        Assert.That(lifecycle.State, Is.EqualTo(TerminalSessionState.Running));
    }

    [Test]
    public async Task OpenAsync_NonfatalFailureKeepsTheRunningSession() {
        var adapter = new FakeTerminalSessionAdapter();
        var lifecycle = new TerminalSessionLifecycle(adapter);
        var changes = new List<TerminalSessionLifecycleEventArgs>();
        lifecycle.Changed += (_, args) => changes.Add(args);
        await lifecycle.OpenAsync(CancellationToken.None);
        lifecycle.ProcessFailed(new IOException("nonfatal render failure"), sessionIsRunning: true);

        Assert.That(lifecycle.State, Is.EqualTo(TerminalSessionState.Running));
        await lifecycle.OpenAsync(CancellationToken.None);
        Assert.That(adapter.StartCount, Is.EqualTo(1));
        Assert.That(changes.FindAll(args => args.State == TerminalSessionState.Failed), Has.Count.EqualTo(1));
        Assert.That(changes[^1].FailureId, Is.GreaterThan(0));
        await lifecycle.CloseAsync(CancellationToken.None);
    }

    [Test]
    public async Task ProcessFailed_DistinctRuntimeErrorsAndFatalFollowupGetFailureIds() {
        var adapter = new FakeTerminalSessionAdapter();
        var lifecycle = new TerminalSessionLifecycle(adapter);
        var changes = new List<TerminalSessionLifecycleEventArgs>();
        lifecycle.Changed += (_, args) => changes.Add(args);
        await lifecycle.OpenAsync(CancellationToken.None);
        var first = new IOException("first nonfatal error");
        var second = new IOException("second nonfatal error");
        var fatal = new IOException("fatal process error");
        lifecycle.ProcessFailed(first, sessionIsRunning: true);
        lifecycle.ProcessFailed(first, sessionIsRunning: true);
        lifecycle.ProcessFailed(second, sessionIsRunning: true);
        lifecycle.ProcessFailed(fatal, sessionIsRunning: false);

        var failures = changes.FindAll(args => args.State == TerminalSessionState.Failed);
        Assert.That(failures, Has.Count.EqualTo(3));
        Assert.That(failures[0].FailureId, Is.LessThan(failures[1].FailureId));
        Assert.That(failures[1].FailureId, Is.LessThan(failures[2].FailureId));
        Assert.That(failures[0].OperationId, Is.EqualTo(failures[2].OperationId));
        Assert.That(lifecycle.State, Is.EqualTo(TerminalSessionState.Failed));
        await lifecycle.OpenAsync(CancellationToken.None);

        Assert.That(adapter.StartCount, Is.EqualTo(2));
        Assert.That(adapter.StopCount, Is.EqualTo(1));
        Assert.That(lifecycle.State, Is.EqualTo(TerminalSessionState.Running));
    }

    [Test]
    public async Task StartException_AfterNonfatalFailureEventStillMarksLifecycleFailed() {
        var adapter = new FakeTerminalSessionAdapter();
        var lifecycle = new TerminalSessionLifecycle(adapter);
        var changes = new List<TerminalSessionLifecycleEventArgs>();
        lifecycle.Changed += (_, args) => changes.Add(args);
        var startFailure = new IOException("start failed");
        adapter.StartWork = () => {
            lifecycle.ProcessFailed(new IOException("nonfatal start notification"), sessionIsRunning: true);
            return Task.FromException(startFailure);
        };

        await Assert.ThrowsAsync<IOException>(async () =>
            await lifecycle.OpenAsync(CancellationToken.None));

        Assert.That(lifecycle.State, Is.EqualTo(TerminalSessionState.Failed));
        Assert.That(changes.FindAll(args => args.State == TerminalSessionState.Failed), Has.Count.EqualTo(1));
        adapter.StartWork = null;
        await lifecycle.OpenAsync(CancellationToken.None);

        Assert.That(adapter.StartCount, Is.EqualTo(2));
        Assert.That(adapter.StopCount, Is.EqualTo(1));
    }

    [Test]
    public async Task ProcessFailed_AfterExitRetainsExitCodeAndPublishesFailure() {
        var adapter = new FakeTerminalSessionAdapter();
        var lifecycle = new TerminalSessionLifecycle(adapter);
        var changes = new List<TerminalSessionLifecycleEventArgs>();
        lifecycle.Changed += (_, args) => changes.Add(args);
        await lifecycle.OpenAsync(CancellationToken.None);
        lifecycle.ProcessExited(29);
        var failure = new InvalidOperationException("exit observation failed");
        lifecycle.ProcessFailed(failure, sessionIsRunning: false);

        Assert.That(lifecycle.State, Is.EqualTo(TerminalSessionState.Failed));
        Assert.That(changes[^1].ExitCode, Is.EqualTo(29));
        Assert.That(changes[^1].Exception, Is.SameAs(failure));
        Assert.That(changes.FindAll(args => args.State == TerminalSessionState.Failed), Has.Count.EqualTo(1));
    }

    private sealed class FakeTerminalSessionAdapter : ITerminalSessionAdapter {
        internal TaskCompletionSource StartEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource StartGate { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource StopCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource StopEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Func<Task> StartWork { get; set; }
        internal Func<Task> StopWork { get; set; }
        internal bool DelayStart { get; set; }
        internal int StartCount { get; private set; }
        internal int StopCount { get; private set; }

        public bool IsRunning { get; private set; }

        public async Task StartAsync() {
            StartCount++;
            StartEntered.TrySetResult();
            if (DelayStart) {
                await StartGate.Task;
            }
            if (StartWork is not null) {
                await StartWork();
            }
            IsRunning = true;
        }

        public async Task StopAsync() {
            StopCount++;
            StopEntered.TrySetResult();
            if (StopWork is not null) {
                await StopWork();
            }
            IsRunning = false;
            StopCompleted.TrySetResult();
        }
    }
}
