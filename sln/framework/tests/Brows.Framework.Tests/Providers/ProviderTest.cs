using Brows.Entries;
using Brows.Gui;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Providers;

[TestFixture]
internal sealed class ProviderTest {
    [Test]
    public async Task EndAndDispose_WaitForDetailShutdownBeforeEndingProvider() {
        var provider = new TestProvider();
        var closeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closeRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var detail = new TestDetail(provider, _ => {
            closeStarted.TrySetResult();
            return closeRelease.Task;
        });
        var shown = await provider.Detail.ShowAsync("view", detail, CancellationToken.None);
        Assert.That(shown, Is.True);

        ((IProvider)provider).End();
        provider.Dispose();
        await closeStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(provider.EndCount, Is.Zero);
        Assert.That(provider.DisposeCount, Is.Zero);
        Assert.That(provider.TokenCancelled, Is.False);

        var disposed = provider.DisposeAsync().AsTask();
        closeRelease.TrySetResult();
        await disposed.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(detail.CloseCount, Is.EqualTo(1));
        Assert.That(provider.EndCount, Is.EqualTo(1));
        Assert.That(provider.DisposeCount, Is.EqualTo(1));
        Assert.That(provider.TokenCancelled, Is.True);
    }

    [Test]
    public async Task End_DetailShutdownFailure_RetainsProviderUntilRetry() {
        var provider = new TestProvider();
        var firstCloseStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstCloseRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var detail = new TestDetail(provider, attempt => {
            if (attempt == 1) {
                firstCloseStarted.TrySetResult();
                return firstCloseRelease.Task;
            }
            return Task.CompletedTask;
        });
        var shown = await provider.Detail.ShowAsync("view", detail, CancellationToken.None);
        Assert.That(shown, Is.True);

        ((IProvider)provider).End();
        await firstCloseStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var failedDisposal = provider.DisposeAsync().AsTask();
        firstCloseRelease.TrySetException(new InvalidOperationException("First close failed."));

        var firstError = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await failedDisposal);
        Assert.That(firstError, Is.Not.Null);
        Assert.That(detail.CloseCount, Is.EqualTo(1));
        Assert.That(provider.EndCount, Is.Zero);
        Assert.That(provider.DisposeCount, Is.Zero);
        Assert.That(provider.TokenCancelled, Is.False);

        await provider.DisposeAsync();

        Assert.That(detail.CloseCount, Is.EqualTo(2));
        Assert.That(provider.EndCount, Is.EqualTo(1));
        Assert.That(provider.DisposeCount, Is.EqualTo(1));
        Assert.That(provider.TokenCancelled, Is.True);
    }

    [Test]
    public void Dispose_CompletedEndFailure_ThrowsAndDisposesExactlyOnce() {
        var failure = new InvalidOperationException("End failed.");
        var provider = new TestProvider { EndError = failure };
        var caught = Assert.Throws<InvalidOperationException>(() => provider.Dispose());
        Assert.That(caught, Is.SameAs(failure));
        Assert.That(provider.EndCount, Is.EqualTo(1));
        Assert.That(provider.DisposeCount, Is.EqualTo(1));
        Assert.That(provider.TokenCancelled, Is.True);
        Assert.That(() => provider.Dispose(), Throws.Nothing);
        Assert.That(provider.DisposeCount, Is.EqualTo(1));
    }

    [Test]
    public void Constructor_WithoutEntries_IsNotAProviderOfEntries() {
        using var provider = new ViewProvider();

        Assert.That(provider, Is.Not.InstanceOf<IEntryProvider>());
    }

    [Test]
    public void Constructor_WithEntries_IsAProviderOfEntries() {
        using var provider = new TestProvider();

        Assert.That(provider, Is.InstanceOf<IEntryProvider>());
        Assert.That(((IEntryProvider)provider).Observation, Is.Not.Null);
    }

    [Test]
    public void Focus_WithoutEntries_FocusesController() {
        using var provider = new ViewProvider();
        var controller = new TestController();
        ((IControlled<IProviderController>)provider).Controller = controller;

        var focused = ((IProvider)provider).Focus();

        Assert.That(focused, Is.True);
        Assert.That(controller.FocusCount, Is.EqualTo(1));
    }

    [Test]
    public void Focus_WithoutEntriesOrController_ReturnsFalse() {
        using var provider = new ViewProvider();

        Assert.That(((IProvider)provider).Focus(), Is.False);
    }

    [Test]
    public void Focus_WithEntries_FocusesEntriesInsteadOfController() {
        using var provider = new TestProvider();
        var controller = new TestController();
        ((IControlled<IProviderController>)provider).Controller = controller;

        var focused = ((IProvider)provider).Focus();

        Assert.That(focused, Is.False);
        Assert.That(controller.FocusCount, Is.Zero);
    }

    [Test]
    public void Controller_WhenChanged_RaisesControllerChanged() {
        using var provider = new ViewProvider();
        var controller = new TestController();
        var changes = 0;
        ((IProvider)provider).ControllerChanged += (sender, e) => changes++;

        ((IControlled<IProviderController>)provider).Controller = controller;
        ((IControlled<IProviderController>)provider).Controller = controller;
        provider.DetachController(controller);

        Assert.That(changes, Is.EqualTo(2));
        Assert.That(((IProvider)provider).Focus(), Is.False);
    }

    [Test]
    public void DetachController_WhenControllerWasReplaced_KeepsReplacement() {
        using var provider = new ViewProvider();
        var previous = new TestController();
        var replacement = new TestController();
        ((IControlled<IProviderController>)provider).Controller = previous;
        ((IControlled<IProviderController>)provider).Controller = replacement;

        provider.DetachController(previous);
        ((IProvider)provider).Focus();

        Assert.That(previous.FocusCount, Is.Zero);
        Assert.That(replacement.FocusCount, Is.EqualTo(1));
    }

    [Test]
    public async Task EndAndDispose_WithoutEntries_EndsProvider() {
        var provider = new ViewProvider();

        ((IProvider)provider).End();
        await provider.DisposeAsync();

        Assert.That(provider.TokenCancelled, Is.True);
    }

    private sealed class ViewProvider : Provider {
        internal bool TokenCancelled =>
            Token.IsCancellationRequested;

        public ViewProvider() : base("view") {
        }
    }

    private sealed class TestController : IProviderController {
        public int FocusCount { get; private set; }

        public bool Focus() {
            FocusCount++;
            return true;
        }
    }

    private sealed class TestProvider : Provider<TestEntry, TestConfig> {
        private int _DisposeCount;
        private int _EndCount;

        internal Exception EndError { get; init; }

        internal int DisposeCount =>
            Volatile.Read(ref _DisposeCount);

        internal int EndCount =>
            Volatile.Read(ref _EndCount);

        internal bool TokenCancelled =>
            Token.IsCancellationRequested;

        public TestProvider() : base("test") {
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                Interlocked.Increment(ref _DisposeCount);
            }
            base.Dispose(disposing);
        }

        protected override void End() {
            Interlocked.Increment(ref _EndCount);
            if (EndError is not null) {
                throw EndError;
            }
        }
    }

    private sealed class TestDetail : ProviderDetail, IProviderDetailLifetime {
        private readonly Func<int, Task> _Close;
        private int _CloseCount;

        internal int CloseCount =>
            Volatile.Read(ref _CloseCount);

        public TestDetail(TestProvider provider, Func<int, Task> close) : base(provider) {
            _Close = close;
        }

        public void Invalidate() {
        }

        public Task CloseAsync(CancellationToken token) {
            var attempt = Interlocked.Increment(ref _CloseCount);
            return _Close(attempt);
        }

        protected override ProviderDetail Clone(Provider provider) {
            return null;
        }
    }

    private sealed class TestConfig : EntryConfig {
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
}
