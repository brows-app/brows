using Brows.Entries;
using Brows.Gui;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Providers;

[TestFixture]
internal sealed class ProviderDetailSetTest {
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static TaskCompletionSource NewSignal() {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    [Test]
    public async Task ToggleAsync_TwiceDuringTeardown_ClosesPreview() {
        await using var provider = new TestProvider();
        var gate = NewSignal();
        var terminal = new LifetimeDetail(provider, closeGate: gate);
        var set = provider.Detail;
        Assert.That(await set.ShowAsync("terminal", terminal, CancellationToken.None), Is.True);

        try {
            var first = set.ToggleAsync("preview", () => new TestDetail(provider), CancellationToken.None);
            await terminal.CloseStarted.Task.WaitAsync(Timeout);
            var second = set.ToggleAsync("preview", () => new TestDetail(provider), CancellationToken.None);
            gate.TrySetResult();

            Assert.That(await first.WaitAsync(Timeout), Is.True);
            Assert.That(await second.WaitAsync(Timeout), Is.True);
            Assert.That(set.Active, Is.Null);
            Assert.That(terminal.CloseCount, Is.EqualTo(1));
        }
        finally {
            gate.TrySetResult();
        }
    }

    [Test]
    public async Task ToggleAsync_CanceledQueuedRequest_DoesNotChangeLaterDecision() {
        await using var provider = new TestProvider();
        var gate = NewSignal();
        var terminal = new LifetimeDetail(provider, closeGate: gate);
        var set = provider.Detail;
        Assert.That(await set.ShowAsync("terminal", terminal, CancellationToken.None), Is.True);
        using var cancellation = new CancellationTokenSource();
        var canceledFactoryCalls = 0;

        try {
            var first = set.ToggleAsync("preview", () => new TestDetail(provider), CancellationToken.None);
            await terminal.CloseStarted.Task.WaitAsync(Timeout);
            var canceled = set.ToggleAsync("preview", () => {
                canceledFactoryCalls++;
                return new TestDetail(provider);
            }, cancellation.Token);
            cancellation.Cancel();
            var latest = set.ToggleAsync("preview", () => new TestDetail(provider), CancellationToken.None);
            gate.TrySetResult();

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await canceled.WaitAsync(Timeout));
            Assert.That(await first.WaitAsync(Timeout), Is.True);
            Assert.That(await latest.WaitAsync(Timeout), Is.True);
            Assert.That(set.Active, Is.Null);
            Assert.That(canceledFactoryCalls, Is.Zero);
        }
        finally {
            gate.TrySetResult();
        }
    }

    [Test]
    public async Task ToggleAsync_PreCanceledRequest_DoesNotInvokeFactory() {
        await using var provider = new TestProvider();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var factoryCalls = 0;

        Assert.That(async () => {
            await provider.Detail.ToggleAsync("preview", () => {
                factoryCalls++;
                return new TestDetail(provider);
            }, cancellation.Token);
        }, Throws.InstanceOf<OperationCanceledException>());

        Assert.That(factoryCalls, Is.Zero);
        Assert.That(provider.Detail.Active, Is.Null);
    }

    [Test]
    public async Task ToggleAsync_FailedTeardown_RetryStillShowsPreview() {
        await using var provider = new TestProvider();
        var terminal = new LifetimeDetail(
            provider, closeFailure: new InvalidOperationException("close"), closeFailureCount: 1);
        var set = provider.Detail;
        var rejected = new LifetimeDetail(provider);
        Assert.That(await set.ShowAsync("terminal", terminal, CancellationToken.None), Is.True);

        Assert.That(await set.ToggleAsync("preview", () => rejected, CancellationToken.None), Is.False);
        Assert.That(set.Active, Is.SameAs(terminal));
        Assert.That(rejected.CloseCount, Is.EqualTo(1));
        Assert.That(await set.ToggleAsync("preview", () => new TestDetail(provider), CancellationToken.None), Is.True);
        Assert.That(set.ActiveKey, Is.EqualTo("preview"));
        Assert.That(terminal.CloseCount, Is.EqualTo(2));
    }

    [Test]
    public async Task ToggleAsync_FactoryCancelsToken_RetiresCandidate() {
        await using var provider = new TestProvider();
        var set = provider.Detail;
        using var cancellation = new CancellationTokenSource();
        var candidate = new LifetimeDetail(provider);

        await Assert.ThrowsAsync<OperationCanceledException>(async () => {
            await set.ToggleAsync("preview", () => {
                cancellation.Cancel();
                return candidate;
            }, cancellation.Token);
        });

        Assert.That(set.Active, Is.Null);
        Assert.That(candidate.CloseCount, Is.EqualTo(1));
    }

    [Test]
    public async Task ToggleAsync_FactoryStartsShutdown_RetiresCandidateBeforeShutdownCompletes() {
        await using var provider = new TestProvider();
        var set = provider.Detail;
        var closeGate = NewSignal();
        var candidate = new LifetimeDetail(provider, closeGate: closeGate);
        Task shutdown = null;

        try {
            var toggle = set.ToggleAsync("preview", () => {
                shutdown = set.ShutdownAsync();
                return candidate;
            }, CancellationToken.None);
            await candidate.CloseStarted.Task.WaitAsync(Timeout);
            Assert.That(shutdown.IsCompleted, Is.False);
            closeGate.TrySetResult();
            Assert.That(await toggle.WaitAsync(Timeout), Is.False);
            await shutdown.WaitAsync(Timeout);
            Assert.That(candidate.CloseCount, Is.EqualTo(1));
        }
        finally {
            closeGate.TrySetResult();
        }
    }

    [Test]
    public async Task ToggleAsync_UsesControllerForFactoryAndTransition() {
        await using var provider = new TestProvider();
        var set = provider.Detail;
        var calls = 0;
        var inController = false;
        var factoryWasDispatched = false;
        var controller = new Mock<IProviderDetailSetController>();
        controller.Setup(item => item.InvokeAsync(It.IsAny<Func<Task>>())).Returns(async (Func<Task> work) => {
            calls++;
            inController = true;
            try {
                await work();
            }
            finally {
                inController = false;
            }
        });
        ((IControlled<IProviderDetailSetController>)set).Controller = controller.Object;

        var shown = await set.ToggleAsync("preview", () => {
            factoryWasDispatched = inController;
            return new TestDetail(provider);
        }, CancellationToken.None);

        Assert.That(shown, Is.True);
        Assert.That(factoryWasDispatched, Is.True);
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public async Task ToggleAsync_FactoryReturnsForeignDetail_LeavesBothProvidersUntouched() {
        await using var provider = new TestProvider();
        await using var otherProvider = new TestProvider();
        var set = provider.Detail;
        var active = new LifetimeDetail(provider);
        var foreign = new LifetimeDetail(otherProvider);
        Assert.That(await set.ShowAsync("active", active, CancellationToken.None), Is.True);

        await Assert.ThrowsAsync<ArgumentException>(async () => {
            await set.ToggleAsync("preview", () => foreign, CancellationToken.None);
        });

        Assert.That(set.Active, Is.SameAs(active));
        Assert.That(active.CloseCount, Is.Zero);
        Assert.That(foreign.InvalidateCount, Is.Zero);
        Assert.That(foreign.CloseCount, Is.Zero);
    }

    [Test]
    public async Task ShowAsync_AdmissionNotificationThrows_ShutdownDoesNotWaitForStrandedShow() {
        await using var provider = new TestProvider();
        var set = provider.Detail;
        var candidate = new LifetimeDetail(provider);
        var failure = new InvalidOperationException("observer");
        PropertyChangedEventHandler observer = (_, args) => {
            if (args.PropertyName == nameof(ProviderDetailSet.Generation)) {
                throw failure;
            }
        };
        set.PropertyChanged += observer;
        try {
            Assert.That(await set.ShowAsync("preview", candidate, CancellationToken.None), Is.False);
        }
        finally {
            set.PropertyChanged -= observer;
        }

        await set.ShutdownAsync().WaitAsync(Timeout);
        Assert.That(set.Active, Is.Null);
        Assert.That(candidate.InvalidateCount, Is.EqualTo(1));
        Assert.That(candidate.CloseCount, Is.EqualTo(1));
    }

    [Test]
    public async Task CloseAsync_StaleDetail_LeavesReplacementActive() {
        await using var provider = new TestProvider();
        var active = new LifetimeDetail(provider);
        var replacement = new LifetimeDetail(provider);
        var set = provider.Detail;

        Assert.That(await set.ShowAsync("preview", active, CancellationToken.None), Is.True);
        Assert.That(await set.ShowAsync("preview", replacement, CancellationToken.None), Is.True);

        var closed = await set.CloseAsync(active, CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(closed, Is.False);
            Assert.That(set.Active, Is.SameAs(replacement));
            Assert.That(set["preview"], Is.SameAs(replacement));
            Assert.That(active.CloseCount, Is.EqualTo(1));
            Assert.That(replacement.CloseCount, Is.Zero);
        }
    }

    [Test]
    public async Task ShowAsync_ConcurrentReplacement_OnlyLatestDetailBecomesActive() {
        await using var provider = new TestProvider();
        var closeGate = NewSignal();
        var active = new LifetimeDetail(provider, closeGate: closeGate);
        var superseded = new LifetimeDetail(provider);
        var latest = new TestDetail(provider);
        var set = provider.Detail;
        Assert.That(await set.ShowAsync("active", active, CancellationToken.None), Is.True);

        Task<bool> supersededShow = null;
        Task<bool> latestShow = null;
        try {
            supersededShow = set.ShowAsync("superseded", superseded, CancellationToken.None);
            await active.CloseStarted.Task.WaitAsync(Timeout);
            latestShow = set.ShowAsync("latest", latest, CancellationToken.None);
            closeGate.TrySetResult();

            var supersededWasShown = await supersededShow.WaitAsync(Timeout);
            var latestWasShown = await latestShow.WaitAsync(Timeout);

            using (Assert.EnterMultipleScope()) {
                Assert.That(supersededWasShown, Is.False);
                Assert.That(latestWasShown, Is.True);
                Assert.That(set.Active, Is.SameAs(latest));
                Assert.That(set.ActiveKey, Is.EqualTo("latest"));
                Assert.That(active.CloseCount, Is.EqualTo(1));
                Assert.That(superseded.CloseCount, Is.EqualTo(1));
                Assert.That(latest.Provider, Is.SameAs(provider));
            }
        }
        finally {
            closeGate.TrySetResult();
        }
    }

    [Test]
    public async Task CloseAsync_StalePendingCandidate_DoesNotSupersedeLaterShow() {
        await using var provider = new TestProvider();
        var activeCloseGate = NewSignal();
        var active = new LifetimeDetail(provider, closeGate: activeCloseGate);
        var candidate = new LifetimeDetail(provider);
        var latest = new TestDetail(provider);
        var set = provider.Detail;
        Assert.That(await set.ShowAsync("active", active, CancellationToken.None), Is.True);

        Task<bool> showCandidate = null;
        Task<bool> closeCandidate = null;
        Task<bool> showLatest = null;
        try {
            showCandidate = set.ShowAsync("candidate", candidate, CancellationToken.None);
            await active.CloseStarted.Task.WaitAsync(Timeout);
            closeCandidate = set.CloseAsync(candidate, CancellationToken.None);
            showLatest = set.ShowAsync("latest", latest, CancellationToken.None);
            activeCloseGate.TrySetResult();

            var candidateWasShown = await showCandidate.WaitAsync(Timeout);
            var candidateWasClosed = await closeCandidate.WaitAsync(Timeout);
            var latestWasShown = await showLatest.WaitAsync(Timeout);

            using (Assert.EnterMultipleScope()) {
                Assert.That(candidateWasShown, Is.False);
                Assert.That(candidateWasClosed, Is.False);
                Assert.That(latestWasShown, Is.True);
                Assert.That(set.Active, Is.SameAs(latest));
                Assert.That(set.ActiveKey, Is.EqualTo("latest"));
                Assert.That(candidate.CloseCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(set.Error, Is.Null);
            }
        }
        finally {
            activeCloseGate.TrySetResult();
        }
    }

    [Test]
    public async Task ShowAsync_FailedTeardown_RetainsActiveDetailAndCanRetry() {
        await using var provider = new TestProvider();
        var failure = new InvalidOperationException("The detail could not be closed.");
        var active = new LifetimeDetail(provider, closeFailure: failure, closeFailureCount: 1);
        var replacement = new TestDetail(provider);
        var set = provider.Detail;
        Assert.That(await set.ShowAsync("active", active, CancellationToken.None), Is.True);

        var firstAttempt = await set.ShowAsync("replacement", replacement, CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(firstAttempt, Is.False);
            Assert.That(set.Active, Is.SameAs(active));
            Assert.That(set.Error, Is.SameAs(failure));
            Assert.That(set.IsCurrent(active, set.Generation), Is.True);
            Assert.That(active.CloseCount, Is.EqualTo(1));
        }

        var retry = await set.ShowAsync("replacement", replacement, CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(retry, Is.True);
            Assert.That(set.Active, Is.SameAs(replacement));
            Assert.That(set.Error, Is.Null);
            Assert.That(active.CloseCount, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task ShowAsync_CanceledWhileWaiting_CompletesCandidateCleanupFirst() {
        await using var provider = new TestProvider();
        var activeCloseGate = NewSignal();
        var candidateCloseGate = NewSignal();
        var active = new LifetimeDetail(provider, closeGate: activeCloseGate);
        var candidate = new LifetimeDetail(provider, closeGate: candidateCloseGate);
        var set = provider.Detail;
        Assert.That(await set.ShowAsync("active", active, CancellationToken.None), Is.True);

        var closeActive = set.CloseAsync(active, CancellationToken.None);
        Task<bool> showCandidate = null;
        using var cancellation = new CancellationTokenSource();
        try {
            await active.CloseStarted.Task.WaitAsync(Timeout);
            showCandidate = set.ShowAsync("candidate", candidate, cancellation.Token);
            cancellation.Cancel();
            await candidate.CloseStarted.Task.WaitAsync(Timeout);

            Assert.That(showCandidate.IsCompleted, Is.False);
            candidateCloseGate.TrySetResult();
            await Assert.ThrowsAsync<OperationCanceledException>(async () => {
                await showCandidate;
            });

            activeCloseGate.TrySetResult();
            Assert.That(await closeActive.WaitAsync(Timeout), Is.True);
            Assert.That(set.Active, Is.Null);
            Assert.That(candidate.CloseCount, Is.EqualTo(1));
        }
        finally {
            candidateCloseGate.TrySetResult();
            activeCloseGate.TrySetResult();
        }
    }

    [Test]
    public async Task ShutdownAsync_JoinsCanceledCandidateCleanup() {
        await using var provider = new TestProvider();
        var activeGate = NewSignal();
        var candidateGate = NewSignal();
        var active = new LifetimeDetail(provider, closeGate: activeGate);
        var candidate = new LifetimeDetail(provider, closeGate: candidateGate);
        var set = provider.Detail;
        Assert.That(await set.ShowAsync("active", active, CancellationToken.None), Is.True);
        using var cancellation = new CancellationTokenSource();
        try {
            var close = set.CloseAsync(active, CancellationToken.None);
            await active.CloseStarted.Task.WaitAsync(Timeout);
            var show = set.ShowAsync("candidate", candidate, cancellation.Token);
            cancellation.Cancel();
            await candidate.CloseStarted.Task.WaitAsync(Timeout);
            var shutdown = set.ShutdownAsync();
            activeGate.TrySetResult();
            Assert.That(await close.WaitAsync(Timeout), Is.True);
            await Assert.ThrowsAsync<TimeoutException>(async () => {
                await shutdown.WaitAsync(TimeSpan.FromMilliseconds(100));
            });
            candidateGate.TrySetResult();
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await show.WaitAsync(Timeout));
            await shutdown.WaitAsync(Timeout);
            Assert.That(candidate.CloseCount, Is.EqualTo(1));
        }
        finally {
            activeGate.TrySetResult();
            candidateGate.TrySetResult();
        }
    }

    [Test]
    public async Task ShowAsync_OverlappingSamePendingInstance_DoesNotRetireAcceptedDetail() {
        await using var provider = new TestProvider();
        var activeGate = NewSignal();
        var active = new LifetimeDetail(provider, closeGate: activeGate);
        var candidate = new LifetimeDetail(provider);
        var set = provider.Detail;
        Assert.That(await set.ShowAsync("active", active, CancellationToken.None), Is.True);
        try {
            var first = set.ShowAsync("candidate", candidate, CancellationToken.None);
            await active.CloseStarted.Task.WaitAsync(Timeout);
            var second = set.ShowAsync("candidate", candidate, CancellationToken.None);
            activeGate.TrySetResult();
            Assert.That(await first.WaitAsync(Timeout), Is.False);
            Assert.That(await second.WaitAsync(Timeout), Is.True);
            Assert.That(set.Active, Is.SameAs(candidate));
            Assert.That(candidate.InvalidateCount, Is.Zero);
            Assert.That(candidate.CloseCount, Is.Zero);
        }
        finally {
            activeGate.TrySetResult();
        }
    }

    [Test]
    public async Task ShutdownAsync_FailedCanceledCandidateRetirement_CanRetry() {
        await using var provider = new TestProvider();
        var activeGate = NewSignal();
        var candidateGate = NewSignal();
        var failure = new InvalidOperationException("Candidate close failed.");
        var active = new LifetimeDetail(provider, closeGate: activeGate);
        var candidate = new LifetimeDetail(provider, candidateGate, failure, closeFailureCount: 1);
        var set = provider.Detail;
        Assert.That(await set.ShowAsync("active", active, CancellationToken.None), Is.True);
        using var cancellation = new CancellationTokenSource();
        try {
            var close = set.CloseAsync(active, CancellationToken.None);
            await active.CloseStarted.Task.WaitAsync(Timeout);
            var show = set.ShowAsync("candidate", candidate, cancellation.Token);
            cancellation.Cancel();
            await candidate.CloseStarted.Task.WaitAsync(Timeout);
            var shutdown = set.ShutdownAsync();
            activeGate.TrySetResult();
            candidateGate.TrySetResult();
            await close.WaitAsync(Timeout);
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await show.WaitAsync(Timeout));
            var shutdownFailure = await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                await shutdown.WaitAsync(Timeout);
            });
            Assert.That(shutdownFailure, Is.SameAs(failure));
            Assert.That(set.Error, Is.SameAs(failure));
            Assert.That(candidate.CloseCount, Is.EqualTo(1));
            await set.ShutdownAsync().WaitAsync(Timeout);
            Assert.That(candidate.CloseCount, Is.EqualTo(2));
            Assert.That(set.Error, Is.Null);
        }
        finally {
            activeGate.TrySetResult();
            candidateGate.TrySetResult();
        }
    }

    [Test]
    public async Task ShowAsync_CandidateRetirementStarted_RejectsSameInstance() {
        await using var provider = new TestProvider();
        var activeGate = NewSignal();
        var candidateGate = NewSignal();
        var active = new LifetimeDetail(provider, closeGate: activeGate);
        var candidate = new LifetimeDetail(provider, closeGate: candidateGate);
        var set = provider.Detail;
        Assert.That(await set.ShowAsync("active", active, CancellationToken.None), Is.True);
        using var cancellation = new CancellationTokenSource();
        try {
            var close = set.CloseAsync(active, CancellationToken.None);
            await active.CloseStarted.Task.WaitAsync(Timeout);
            var canceledShow = set.ShowAsync("candidate", candidate, cancellation.Token);
            cancellation.Cancel();
            await candidate.CloseStarted.Task.WaitAsync(Timeout);

            var shownAgain = await set.ShowAsync("candidate", candidate, CancellationToken.None).WaitAsync(Timeout);
            Assert.That(shownAgain, Is.False);
            activeGate.TrySetResult();
            candidateGate.TrySetResult();
            await close.WaitAsync(Timeout);
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await canceledShow.WaitAsync(Timeout));
            Assert.That(set.Active, Is.Null);
        }
        finally {
            activeGate.TrySetResult();
            candidateGate.TrySetResult();
        }
    }

    [Test]
    public async Task Inherit_DoesNotCopyDuringTransitionAndCopiesStableActiveDetail() {
        await using var source = new TestProvider();
        await using var destination = new TestProvider();
        var closeGate = NewSignal();
        var previous = new LifetimeDetail(source, closeGate: closeGate);
        var active = new TestDetail(source);
        Assert.That(await source.Detail.ShowAsync("previous", previous, CancellationToken.None), Is.True);

        Task<bool> showActive = null;
        try {
            showActive = source.Detail.ShowAsync("active", active, CancellationToken.None);
            await previous.CloseStarted.Task.WaitAsync(Timeout);

            destination.Detail.Inherit(source);
            Assert.That(destination.Detail.Active, Is.Null);

            closeGate.TrySetResult();
            Assert.That(await showActive.WaitAsync(Timeout), Is.True);
            destination.Detail.Inherit(source);

            var inherited = destination.Detail.Active;
            using (Assert.EnterMultipleScope()) {
                Assert.That(inherited, Is.TypeOf<TestDetail>());
                Assert.That(inherited, Is.Not.SameAs(active));
                Assert.That(inherited.Provider, Is.SameAs(destination));
                Assert.That(destination.Detail.ActiveKey, Is.EqualTo("active"));
                Assert.That(source.Detail.Active, Is.SameAs(active));
            }
        }
        finally {
            closeGate.TrySetResult();
        }
    }

    [Test]
    public async Task ProviderFocus_UsesOptionalCapabilityAndDefaultsWhenAbsent() {
        await using var provider = new TestProvider();
        var plainDetail = new TestDetail(provider);
        var optionalDetail = new FocusDetail(provider) {
            HasKeyboardFocusValue = true,
            RequestFocusResult = true,
        };
        var plainFocus = (IProviderFocus)plainDetail;
        var optionalFocus = (IProviderFocus)optionalDetail;

        using (Assert.EnterMultipleScope()) {
            Assert.That(plainFocus.Get(), Is.False);
            Assert.That(plainFocus.Set(), Is.False);
            Assert.That(optionalFocus.Get(), Is.True);
            Assert.That(optionalFocus.Set(), Is.True);
            Assert.That(optionalDetail.RequestFocusCount, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task IsCurrent_NullDetailWithNoActiveDetail_ReturnsFalse() {
        await using var provider = new TestProvider();

        Assert.That(provider.Detail.IsCurrent(null, provider.Detail.Generation), Is.False);
    }

    [Test]
    public async Task ShowAsync_SameDetailAndKey_DoesNotChangeGeneration() {
        await using var provider = new TestProvider();
        var detail = new TestDetail(provider);
        var set = provider.Detail;
        Assert.That(await set.ShowAsync("preview", detail, CancellationToken.None), Is.True);
        var generation = set.Generation;

        var shownAgain = await set.ShowAsync("preview", detail, CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(shownAgain, Is.True);
            Assert.That(set.Active, Is.SameAs(detail));
            Assert.That(set.ActiveKey, Is.EqualTo("preview"));
            Assert.That(set.Generation, Is.EqualTo(generation));
        }
    }

    [Test]
    public async Task ShowAsync_FocusGetterThrows_DoesNotPreventCleanupOrReplacement() {
        await using var provider = new TestProvider();
        var active = new FocusDetail(provider) {
            FocusError = new InvalidOperationException("The focus getter failed."),
            ThrowOnRead = true,
        };
        var replacement = new TestDetail(provider);
        var set = provider.Detail;
        Assert.That(await set.ShowAsync("active", active, CancellationToken.None), Is.True);

        var replaced = await set.ShowAsync("replacement", replacement, CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(replaced, Is.True);
            Assert.That(active.CloseCount, Is.EqualTo(1));
            Assert.That(set.Active, Is.SameAs(replacement));
            Assert.That(set.Error, Is.Null);
            Assert.That(set.IsCurrent(replacement, set.Generation), Is.True);
        }
    }

    [Test]
    public async Task CloseAsync_FocusGetterThrows_DoesNotPreventCleanupOrStability() {
        await using var provider = new TestProvider();
        var active = new FocusDetail(provider) {
            FocusError = new InvalidOperationException("The focus getter failed."),
            ThrowOnRead = true,
        };
        var set = provider.Detail;
        Assert.That(await set.ShowAsync("active", active, CancellationToken.None), Is.True);

        var closed = await set.CloseAsync(active, CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(closed, Is.True);
            Assert.That(active.CloseCount, Is.EqualTo(1));
            Assert.That(set.Active, Is.Null);
            Assert.That(set.Error, Is.Null);
        }

        var replacement = new TestDetail(provider);
        Assert.That(await set.ShowAsync("replacement", replacement, CancellationToken.None), Is.True);
        Assert.That(set.IsCurrent(replacement, set.Generation), Is.True);
    }

    [Test]
    public async Task ShutdownAsync_RejectedDispatch_RetainsViewAndPermitsRetry() {
        await using var provider = new TestProvider();
        var detail = new LifetimeDetail(provider);
        var set = provider.Detail;
        await set.ShowAsync("active", detail, CancellationToken.None);
        var rejection = new InvalidOperationException("The dispatcher rejected this request.");
        var attempts = 0;
        var controller = new Mock<IProviderDetailSetController>();
        controller.Setup(item => item.InvokeAsync(It.IsAny<Func<Task>>())).Returns((Func<Task> work) => {
            var attempt = Interlocked.Increment(ref attempts);
            if (attempt == 1) {
                return Task.FromException(rejection);
            }
            return work();
        });
        ((IControlled<IProviderDetailSetController>)set).Controller = controller.Object;

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await set.ShutdownAsync().WaitAsync(Timeout);
        });
        Assert.That(failure, Is.SameAs(rejection));
        Assert.That(set.Active, Is.SameAs(detail));
        Assert.That(set.Error, Is.SameAs(rejection));
        Assert.That(set.IsCurrent(detail, set.Generation), Is.True);
        Assert.That(detail.CloseCount, Is.Zero);

        var retry = set.ShutdownAsync();
        await retry.WaitAsync(Timeout);
        Assert.That(set.Active, Is.Null);
        Assert.That(set.Error, Is.Null);
        Assert.That(detail.CloseCount, Is.EqualTo(1));
        Assert.That(set.ShutdownAsync(), Is.SameAs(retry));
        Assert.That(attempts, Is.EqualTo(2));
    }
    private sealed class TestProvider : Provider<TestEntry, TestConfig> {
        public TestProvider() : base("test") {
        }
    }

    private sealed class TestConfig : EntryConfig {
        public TestConfig() {
        }
    }

    private sealed class TestEntry : Entry<TestProvider> {
        public TestEntry(TestProvider provider, string name) : base(provider) {
            Name = name;
        }

        public override string ID =>
            Name;

        public override string Name {
            get;
        }
    }

    private sealed class TestDetail : ProviderDetail {
        public TestDetail(Provider provider) : base(provider) {
        }

        protected override ProviderDetail Clone(Provider provider) {
            return new TestDetail(provider);
        }
    }

    private sealed class LifetimeDetail : ProviderDetail, IProviderDetailLifetime {
        private readonly TaskCompletionSource CloseStartedValue = NewSignal();
        private readonly TaskCompletionSource CloseGate;
        private readonly Exception CloseFailure;
        private readonly int CloseFailureCount;

        private int CloseCountValue;
        private int InvalidateCountValue;

        public LifetimeDetail(Provider provider,
                              TaskCompletionSource closeGate = null,
                              Exception closeFailure = null,
                              int closeFailureCount = 0)
        : base(provider) {
            CloseGate = closeGate;
            CloseFailure = closeFailure;
            CloseFailureCount = closeFailureCount;
        }

        public int CloseCount =>
            Volatile.Read(ref CloseCountValue);

        public int InvalidateCount =>
            Volatile.Read(ref InvalidateCountValue);

        public TaskCompletionSource CloseStarted =>
            CloseStartedValue;

        protected override ProviderDetail Clone(Provider provider) {
            return new LifetimeDetail(provider);
        }

        public void Invalidate() {
            Interlocked.Increment(ref InvalidateCountValue);
        }

        public Task CloseAsync(CancellationToken token) {
            var count = Interlocked.Increment(ref CloseCountValue);
            CloseStartedValue.TrySetResult();
            return Close(count);
        }

        private async Task Close(int count) {
            if (CloseGate is not null) {
                await CloseGate.Task;
            }
            if (CloseFailure is not null && count <= CloseFailureCount) {
                throw CloseFailure;
            }
        }
    }

    private sealed class FocusDetail : ProviderDetail, IProviderDetailFocus, IProviderDetailLifetime {
        private int CloseCountValue;

        public FocusDetail(Provider provider) : base(provider) {
        }

        public bool HasKeyboardFocusValue { get; set; }

        public bool RequestFocusResult { get; set; }

        public Exception FocusError { get; set; }

        public bool ThrowOnRead { get; set; }

        public int CloseCount =>
            Volatile.Read(ref CloseCountValue);

        public int RequestFocusCount { get; private set; }

        protected override ProviderDetail Clone(Provider provider) {
            return new FocusDetail(provider);
        }

        public void Invalidate() {
        }

        public Task CloseAsync(CancellationToken token) {
            Interlocked.Increment(ref CloseCountValue);
            return Task.CompletedTask;
        }

        bool IProviderDetailFocus.HasKeyboardFocus {
            get {
                if (ThrowOnRead) {
                    throw FocusError ?? new InvalidOperationException("The focus getter failed.");
                }
                return HasKeyboardFocusValue;
            }
        }

        bool IProviderDetailFocus.RequestFocus() {
            RequestFocusCount++;
            return RequestFocusResult;
        }
    }
}
