using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Entries;

[TestFixture]
internal sealed class EntryDataTest {
    private sealed class Clock : TimeProvider {
        private readonly List<Timer> Timers = [];
        private long Now;

        private sealed class Timer : ITimer {
            private readonly Clock Clock;
            private readonly TimerCallback Callback;
            private readonly object State;

            internal long Due = long.MaxValue;

            internal Timer(Clock clock, TimerCallback callback, object state) {
                Clock = clock;
                Callback = callback;
                State = state;
            }

            internal void Fire() {
                Due = long.MaxValue;
                Callback(State);
            }

            public bool Change(TimeSpan dueTime, TimeSpan period) {
                Due = dueTime == Timeout.InfiniteTimeSpan
                    ? long.MaxValue
                    : Clock.Now + (long)dueTime.TotalMilliseconds;
                return true;
            }

            public void Dispose() {
                Due = long.MaxValue;
            }

            public ValueTask DisposeAsync() {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }

        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() {
            return Now;
        }
        public override ITimer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period) {
            var timer = new Timer(this, callback, state);
            Timers.Add(timer);
            timer.Change(dueTime, period);
            return timer;
        }
        internal void Advance(int milliseconds) {
            Now += milliseconds;
            foreach (var timer in Timers.ToArray()) {
                if (timer.Due <= Now) {
                    timer.Fire();
                }
            }
        }
    }

    [Test]
    public async Task Progress_KeepsLatestValueAndCombinesNotifications() {
        var clock = new Clock();
        var completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<object> report = null;
        var definition = new Mock<IEntryDataDefinition>();
        definition.Setup(d => d.GetValue(
            It.IsAny<IEntry>(), It.IsAny<Action<object>>(), It.IsAny<CancellationToken>()))
            .Returns((IEntry _, Action<object> progress, CancellationToken _) => {
                report = progress;
                return completion.Task;
            });
        var data = new EntryData(Mock.Of<IEntry>(), definition.Object, clock, CancellationToken.None);
        var values = new List<object>();
        ((INotifyPropertyChanged)data).PropertyChanged += (_, e) => {
            if (e.PropertyName == "Display") {
                values.Add(data.Value);
            }
        };
        var ready = ((IEntryData)data).Ready;
        report(1);
        for (var i = 2; i <= 1000; i++) {
            report(i);
        }
        Assert.That(data.Value, Is.EqualTo(1000));
        Assert.That(values, Is.EqualTo(new[] { 1 }));
        clock.Advance(99);
        Assert.That(values, Has.Count.EqualTo(1));
        clock.Advance(1);
        Assert.That(values, Is.EqualTo(new[] { 1, 1000 }));
        for (var cycle = 0; cycle < 3; cycle++) {
            report(2000 + cycle);
            clock.Advance(100);
            Assert.That(values[^1], Is.EqualTo(2000 + cycle));
            Assert.That(values, Has.Count.EqualTo(3 + cycle));
        }
        report(1001);
        completion.SetResult(1001);
        await ready.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(values, Is.EqualTo(new[] { 1, 1000, 2000, 2001, 2002, 1001 }));
        clock.Advance(100);
        Assert.That(values, Has.Count.EqualTo(6));
    }

    [Test]
    public async Task Refresh_OldProgressAndFinalCannotReplaceNewLoad() {
        var old = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<object> report = null;
        var definition = new Mock<IEntryDataDefinition>();
        var calls = 0;
        definition.Setup(d => d.GetValue(
            It.IsAny<IEntry>(), It.IsAny<Action<object>>(), It.IsAny<CancellationToken>()))
            .Returns((IEntry _, Action<object> progress, CancellationToken _) => {
                calls++;
                report ??= progress;
                return calls == 1 ? old.Task : Task.FromResult<object>("new");
            });
        var data = new EntryData(Mock.Of<IEntry>(), definition.Object, CancellationToken.None);
        var previous = ((IEntryData)data).Ready;
        report("old progress");
        await ((IEntryData)data).Refresh(CancellationToken.None);
        await ((IEntryData)data).Ready;
        report("stale");
        old.SetResult("old result");
        await previous.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(data.Value, Is.EqualTo("new"));
        Assert.That(data.Accessing, Is.False);
        Assert.That(calls, Is.EqualTo(2));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Ready_CommitsTerminalOutcomeBeforeCompleting(bool fails) {
        var definition = new Mock<IEntryDataDefinition>();
        definition.Setup(d => d.GetValue(
            It.IsAny<IEntry>(), It.IsAny<Action<object>>(), It.IsAny<CancellationToken>()))
            .Returns((IEntry _, Action<object> progress, CancellationToken _) => {
                progress("intermediate");
                return fails ? Task.FromException<object>(new InvalidOperationException("failed")) :
                    Task.FromResult<object>("final");
            });
        var data = new EntryData(Mock.Of<IEntry>(), definition.Object, CancellationToken.None);
        Task reentrant = null;
        ((INotifyPropertyChanged)data).PropertyChanged += (_, e) => {
            if (e.PropertyName == "Value") {
                reentrant = ((IEntryData)data).Ready;
            }
        };
        var ready = ((IEntryData)data).Ready;
        await ready;
        Assert.That(reentrant, Is.SameAs(ready));
        Assert.That(data.Value, Is.EqualTo(fails ? null : "final"));
        Assert.That(data.AccessError, Is.EqualTo(fails));
        Assert.That(data.Accessing, Is.False);
    }

    [Test]
    public async Task Cancellation_StopsProgressEvenWhenLoaderIgnoresToken() {
        using var cancellation = new CancellationTokenSource();
        var completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<object> report = null;
        var definition = new Mock<IEntryDataDefinition>();
        definition.Setup(d => d.GetValue(
            It.IsAny<IEntry>(), It.IsAny<Action<object>>(), It.IsAny<CancellationToken>()))
            .Returns((IEntry _, Action<object> progress, CancellationToken _) => {
                report = progress;
                return completion.Task;
            });
        var clock = new Clock();
        var data = new EntryData(Mock.Of<IEntry>(), definition.Object, clock, cancellation.Token);
        var ready = ((IEntryData)data).Ready;
        report(1);
        report(2);
        cancellation.Cancel();
        report(3);
        Assert.That(data.Value, Is.EqualTo(2));
        completion.SetResult(4);
        await ready.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Advance(100);
        Assert.That(data.Value, Is.Null);
        Assert.That(data.AccessCanceled, Is.True);
        Assert.That(data.AccessError, Is.False);
        Assert.That(data.AccessingFlag, Is.False);
    }

    [Test]
    public async Task Refresh_ReadyWaitsForRefreshAndFailureCanBeRetried() {
        var refresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var definition = new Mock<IEntryDataDefinition>();
        definition.Setup(d => d.RefreshValue(It.IsAny<IEntry>(), It.IsAny<CancellationToken>()))
            .Returns(refresh.Task);
        definition.Setup(d => d.GetValue(
            It.IsAny<IEntry>(), It.IsAny<Action<object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("new");
        var data = new EntryData(Mock.Of<IEntry>(), definition.Object, CancellationToken.None);
        var refreshing = ((IEntryData)data).Refresh(CancellationToken.None);
        var ready = ((IEntryData)data).Ready;
        definition.Verify(d => d.GetValue(It.IsAny<IEntry>(), It.IsAny<Action<object>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        refresh.SetException(new InvalidOperationException("refresh failed"));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await refreshing);
        await ready.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(data.AccessError, Is.True);
        definition.Setup(d => d.RefreshValue(It.IsAny<IEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        await ((IEntryData)data).Refresh(CancellationToken.None);
        await ((IEntryData)data).Ready;
        Assert.That(data.Value, Is.EqualTo("new"));
        Assert.That(data.AccessError, Is.False);
    }

    [Test]
    public async Task Notifications_ReentrantRefreshSuppressesOldDisplayAndCompletion() {
        var completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<object> report = null;
        var definition = new Mock<IEntryDataDefinition>();
        definition.Setup(d => d.GetValue(
            It.IsAny<IEntry>(), It.IsAny<Action<object>>(), It.IsAny<CancellationToken>()))
            .Returns((IEntry _, Action<object> progress, CancellationToken _) => {
                report = progress;
                return completion.Task;
            });
        var data = new EntryData(Mock.Of<IEntry>(), definition.Object, CancellationToken.None);
        var displayCount = 0;
        Task refreshing = null;
        ((INotifyPropertyChanged)data).PropertyChanged += (_, e) => {
            if (e.PropertyName == "Value") {
                refreshing = ((IEntryData)data).Refresh(CancellationToken.None);
            }
            if (e.PropertyName == "Display") {
                displayCount++;
            }
        };
        var ready = ((IEntryData)data).Ready;
        report("progress");
        await refreshing;
        completion.SetResult("obsolete");
        await ready.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(data.Value, Is.EqualTo("progress"));
        Assert.That(displayCount, Is.Zero);
    }

    [Test]
    public async Task Refresh_CanceledQueuedRefreshDoesNotInvokeDefinition() {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var later = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var laterStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var definition = new Mock<IEntryDataDefinition>();
        var calls = 0;
        definition.Setup(d => d.RefreshValue(It.IsAny<IEntry>(), It.IsAny<CancellationToken>()))
            .Returns((IEntry _, CancellationToken _) => {
                calls++;
                if (calls == 1) {
                    return pending.Task;
                }
                laterStarted.TrySetResult();
                return later.Task;
            });
        definition.Setup(d => d.GetValue(
            It.IsAny<IEntry>(), It.IsAny<Action<object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("reloaded");
        var data = new EntryData(Mock.Of<IEntry>(), definition.Object, CancellationToken.None);
        var first = ((IEntryData)data).Refresh(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var second = ((IEntryData)data).Refresh(cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAsync<TaskCanceledException>(
            async () => await second.WaitAsync(TimeSpan.FromSeconds(5)));
        var third = ((IEntryData)data).Refresh(CancellationToken.None);
        var ready = ((IEntryData)data).Ready;
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(third.IsCompleted, Is.False);
        Assert.That(ready.IsCompleted, Is.False);
        definition.Verify(d => d.GetValue(It.IsAny<IEntry>(), It.IsAny<Action<object>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        pending.SetResult();
        await first;
        await laterStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(calls, Is.EqualTo(2));
        Assert.That(third.IsCompleted, Is.False);
        Assert.That(ready.IsCompleted, Is.False);
        later.SetResult();
        await third.WaitAsync(TimeSpan.FromSeconds(5));
        await ready.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(data.Value, Is.EqualTo("reloaded"));
        Assert.That(data.AccessError, Is.False);
        Assert.That(data.AccessException, Is.Null);
        definition.Verify(d => d.RefreshValue(It.IsAny<IEntry>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        definition.Verify(d => d.GetValue(It.IsAny<IEntry>(), It.IsAny<Action<object>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Refresh_CanceledRefreshLeavesBarrierLoadable(bool cancelBeforeStarting) {
        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var definition = new Mock<IEntryDataDefinition>();
        var loads = 0;
        definition.Setup(d => d.GetValue(
            It.IsAny<IEntry>(), It.IsAny<Action<object>>(), It.IsAny<CancellationToken>()))
            .Returns((IEntry _, Action<object> _, CancellationToken _) =>
                Task.FromResult<object>(++loads == 1 ? "old" : "reloaded"));
        definition.Setup(d => d.RefreshValue(It.IsAny<IEntry>(), It.IsAny<CancellationToken>()))
            .Returns((IEntry _, CancellationToken token) => {
                refreshStarted.TrySetResult();
                return Task.Delay(Timeout.InfiniteTimeSpan, token);
            });
        var data = new EntryData(Mock.Of<IEntry>(), definition.Object, CancellationToken.None);
        await ((IEntryData)data).Ready.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(data.Value, Is.EqualTo("old"));

        using var cancellation = new CancellationTokenSource();
        if (cancelBeforeStarting) {
            cancellation.Cancel();
        }
        var refreshing = ((IEntryData)data).Refresh(cancellation.Token);
        var ready = ((IEntryData)data).Ready;
        if (!cancelBeforeStarting) {
            await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
        }
        Assert.That(async () => await refreshing.WaitAsync(TimeSpan.FromSeconds(5)),
            Throws.InstanceOf<OperationCanceledException>());

        await ready.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(data.Value, Is.EqualTo("reloaded"));
        Assert.That(data.AccessError, Is.False);
        Assert.That(data.AccessException, Is.Null);
        Assert.That(loads, Is.EqualTo(2));
        definition.Verify(d => d.RefreshValue(It.IsAny<IEntry>(), It.IsAny<CancellationToken>()),
            cancelBeforeStarting ? Times.Never : Times.Once);
    }

    [Test]
    public async Task Refresh_CanceledQueuedRefreshPreservesPredecessorFailureForRetry() {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new InvalidOperationException("earlier refresh failed");
        var definition = new Mock<IEntryDataDefinition>();
        definition.Setup(d => d.RefreshValue(It.IsAny<IEntry>(), It.IsAny<CancellationToken>()))
            .Returns(pending.Task);
        definition.Setup(d => d.GetValue(
            It.IsAny<IEntry>(), It.IsAny<Action<object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("recovered");
        var data = new EntryData(Mock.Of<IEntry>(), definition.Object, CancellationToken.None);
        var first = ((IEntryData)data).Refresh(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var second = ((IEntryData)data).Refresh(cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAsync<TaskCanceledException>(
            async () => await second.WaitAsync(TimeSpan.FromSeconds(5)));

        pending.SetException(failure);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await first);
        await ((IEntryData)data).Ready.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(data.AccessError, Is.True);
        Assert.That(data.AccessException, Is.SameAs(failure));

        definition.Setup(d => d.RefreshValue(It.IsAny<IEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        await ((IEntryData)data).Refresh(CancellationToken.None);
        await ((IEntryData)data).Ready.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(data.Value, Is.EqualTo("recovered"));
        Assert.That(data.AccessError, Is.False);
    }

    [Test]
    public async Task Progress_ConcurrentReportsPublishAtMostOncePerInterval() {
        var completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<object> report = null;
        var definition = new Mock<IEntryDataDefinition>();
        definition.Setup(d => d.GetValue(
            It.IsAny<IEntry>(), It.IsAny<Action<object>>(), It.IsAny<CancellationToken>()))
            .Returns((IEntry _, Action<object> progress, CancellationToken _) => {
                report = progress;
                return completion.Task;
            });
        var data = new EntryData(Mock.Of<IEntry>(), definition.Object, new Clock(), CancellationToken.None);
        var notifications = 0;
        ((INotifyPropertyChanged)data).PropertyChanged += (_, e) => {
            if (e.PropertyName == "Display") {
                Interlocked.Increment(ref notifications);
            }
        };
        var ready = ((IEntryData)data).Ready;
        Parallel.For(1, 1001, value => report(value));
        Assert.That(data.Value, Is.InRange(1, 1000));
        Assert.That(notifications, Is.EqualTo(1));
        completion.SetResult(1001);
        await ready.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(data.Value, Is.EqualTo(1001));
        Assert.That(notifications, Is.EqualTo(2));
    }

    [Test]
    public void Compare_NullOther_ComparesNonNullGreater() {
        var entryData = new EntryData(Mock.Of<IEntry>(), Mock.Of<IEntryDataDefinition>(), CancellationToken.None);

        /*
         * Null compares lower than any non-null entry data.
         */
        Assert.That(((IEntryData)entryData).Compare(null), Is.EqualTo(1));
    }

    [Test]
    public void Compare_ForeignOther_ComparesEqual() {
        var definition = new Mock<IEntryDataDefinition>();
        var entryData = new EntryData(Mock.Of<IEntry>(), definition.Object, CancellationToken.None);
        var other = new Mock<IEntryData>();

        Assert.That(((IEntryData)entryData).Compare(other.Object), Is.EqualTo(0));
    }

    [Test]
    public void Compare_EntryDataOther_DelegatesToCompareValue() {
        var entry = new Mock<IEntry>();
        var otherEntry = new Mock<IEntry>();
        var definition = new Mock<IEntryDataDefinition>();
        definition.Setup(d => d.CompareValue(entry.Object, otherEntry.Object)).Returns(5);
        var entryData = new EntryData(entry.Object, definition.Object, CancellationToken.None);
        var other = new EntryData(otherEntry.Object, definition.Object, CancellationToken.None);

        Assert.That(((IEntryData)entryData).Compare(other), Is.EqualTo(5));
    }
}
