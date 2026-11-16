using Brows.Commands;
using Brows.Gui;
using Brows.IO;
using Brows.Panels;
using Brows.Providers;
using Brows.Triggers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Detail;

[TestFixture]
internal sealed class FileSystemTerminalTest {
    private FileSysSandbox Sandbox;
    private DirectoryInfo TargetDirectory;
    private FileSystemProvider Provider;
    private TestPanel Panel;
    private TestPanelCollection Panels;
    private FileSystemTerminal Terminal;

    [SetUp]
    public void SetUp() {
        Sandbox = new();
        var path = Path.Combine(Sandbox.Root, "start ' & ü");
        TargetDirectory = Directory.CreateDirectory(path);
        Provider = new FileSystemProvider(
            new FileSystemProviderFactory(),
            TargetDirectory,
            caseSensitive: false,
            initialCapacity: 0);
        Panel = new TestPanel {
            Column = 0,
            Provider = Provider
        };
        Panels = new TestPanelCollection {
            Active = Panel
        };
        Panels.Panels.Add(Panel);
        Terminal = new FileSystemTerminal(Provider, Panels, Panel);
    }

    [TearDown]
    public void TearDown() {
        Provider.Dispose();
        Sandbox.Dispose();
    }

    [Test]
    public void FileSystemTerminal_Constructor_SnapshotsLiteralDirectoryAndProvider() {
        using (Assert.EnterMultipleScope()) {
            Assert.That(Terminal.ProviderDirectorySnapshot, Is.EqualTo(TargetDirectory.FullName));
            Assert.That(((IControllingCommandGesture)Terminal).Provider, Is.SameAs(Provider));
            Assert.That(Terminal.IsPanelCurrent, Is.True);
        }
    }

    [Test]
    public void FileSystemTerminal_AllowsOnlyReleaseCommand() {
        var owner = (IControllingCommandGesture)Terminal;

        using (Assert.EnterMultipleScope()) {
            Assert.That(owner.Allows(new TerminalRelease()), Is.True);
            Assert.That(owner.Allows(new TerminalOpen()), Is.False);
            Assert.That(owner.Allows(new TerminalClose()), Is.False);
            Assert.That(owner.Allows(new View()), Is.False);
        }
    }

    [Test]
    public void FileSystemTerminal_Clone_ReturnsNullForAnotherProvider() {
        var otherDirectory = Directory.CreateDirectory(Path.Combine(Sandbox.Root, "other"));
        using var otherProvider = new FileSystemProvider(
            new FileSystemProviderFactory(),
            otherDirectory,
            caseSensitive: false,
            initialCapacity: 0);

        Assert.That(((ProviderDetail)Terminal).For(otherProvider), Is.Null);
    }

    [Test]
    public void FileSystemTerminal_RequestGeneration_LastFocusIntentWinsWithoutInvalidatingSession() {
        var first = Terminal.BeginRequest();
        var release = Terminal.AdvanceReleaseIntent();
        var releaseIntentIsCurrent = Terminal.IsCurrentReleaseIntent(release);
        var second = Terminal.BeginRequest();

        using (Assert.EnterMultipleScope()) {
            Assert.That(Terminal.IsCurrentRequest(first), Is.False);
            Assert.That(releaseIntentIsCurrent, Is.True);
            Assert.That(Terminal.IsCurrentReleaseIntent(release), Is.False);
            Assert.That(Terminal.IsCurrentRequest(second), Is.True);
            Assert.That(Terminal.IsLiveOwner, Is.True);
        }

        var lifetimeToken = Terminal.LifetimeToken;
        ((IProviderDetailLifetime)Terminal).Invalidate();
        var invalidatedRelease = Terminal.AdvanceReleaseIntent();

        using (Assert.EnterMultipleScope()) {
            Assert.That(lifetimeToken.IsCancellationRequested, Is.True);
            Assert.That(Terminal.BeginRequest(), Is.EqualTo(-1));
            Assert.That(Terminal.IsCurrentRequest(second), Is.False);
            Assert.That(Terminal.IsCurrentRequest(invalidatedRelease), Is.False);
            Assert.That(Terminal.IsCurrentReleaseIntent(invalidatedRelease), Is.True);
        }
    }

    [Test]
    public void FileSystemTerminal_IsCurrentOnlyForItsActiveOwningPanel() {
        var otherPanel = new TestPanel {
            Column = 1,
            Provider = new FileSystemProvider(
                new FileSystemProviderFactory(),
                TargetDirectory,
                caseSensitive: false,
                initialCapacity: 0)
        };
        Panels.Panels.Add(otherPanel);
        Panels.Active = otherPanel;

        Assert.That(Terminal.IsPanelCurrent, Is.False);

        Panels.Active = Panel;

        Assert.That(Terminal.IsPanelCurrent, Is.True);
        ((FileSystemProvider)otherPanel.Provider).Dispose();
    }

    [Test]
    public async Task FileSystemTerminal_DetachingStaleControllerKeepsCurrentController() {
        var first = new TestTerminalController();
        var current = new TestTerminalController();
        var controlled = (IControlled<IFileSystemTerminalController>)Terminal;
        controlled.Controller = first;
        controlled.Controller = current;

        var detached = Terminal.DetachController(first);
        var attached = await Terminal.WaitForController(TimeSpan.FromMilliseconds(1), CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(detached, Is.False);
            Assert.That(Terminal.CurrentController, Is.SameAs(current));
            Assert.That(attached, Is.SameAs(current));
        }
    }

    [Test]
    public async Task FileSystemTerminal_LifetimeCloseDelegatesToCurrentController() {
        var controller = new TestTerminalController();
        ((IControlled<IFileSystemTerminalController>)Terminal).Controller = controller;

        await ((IProviderDetailLifetime)Terminal).CloseAsync(CancellationToken.None);

        Assert.That(controller.CloseCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task FileSystemTerminal_LifetimeCloseSharesPendingCleanupAndRemainsClosed() {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var controller = new TestTerminalController { CloseCompletion = completion.Task };
        ((IControlled<IFileSystemTerminalController>)Terminal).Controller = controller;
        var lifetime = (IProviderDetailLifetime)Terminal;

        var first = lifetime.CloseAsync(CancellationToken.None);
        var second = lifetime.CloseAsync(CancellationToken.None);
        var callsBeforeCompletion = controller.CloseCalls;
        completion.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        await lifetime.CloseAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(callsBeforeCompletion, Is.EqualTo(1));
            Assert.That(controller.CloseCalls, Is.EqualTo(1));
            Assert.That(Terminal.LifetimeToken.IsCancellationRequested, Is.True);
        }
    }

    [Test]
    public async Task FileSystemTerminal_LifetimeCloseCallerCancellationDoesNotAbandonCleanup() {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var controller = new TestTerminalController { CloseCompletion = completion.Task };
        ((IControlled<IFileSystemTerminalController>)Terminal).Controller = controller;
        var lifetime = (IProviderDetailLifetime)Terminal;
        using var cancellation = new CancellationTokenSource();

        var canceledWait = lifetime.CloseAsync(cancellation.Token);
        cancellation.Cancel();
        var cancellationObserved = canceledWait.IsCanceled;
        completion.SetResult();
        await lifetime.CloseAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        try {
            await canceledWait;
        }
        catch (OperationCanceledException) {
            cancellationObserved = true;
        }

        using (Assert.EnterMultipleScope()) {
            Assert.That(cancellationObserved, Is.True);
            Assert.That(controller.CloseCalls, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task FileSystemTerminal_LifetimeCloseRetriesFailedCurrentController() {
        var controller = new TestTerminalController { CloseFailureCalls = new HashSet<int> { 1 } };
        ((IControlled<IFileSystemTerminalController>)Terminal).Controller = controller;
        var lifetime = (IProviderDetailLifetime)Terminal;

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await lifetime.CloseAsync(CancellationToken.None));
        await lifetime.CloseAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        await lifetime.CloseAsync(CancellationToken.None);

        Assert.That(controller.CloseCalls, Is.EqualTo(2));
    }

    [Test]
    public async Task FileSystemTerminal_LifetimeCloseAwaitsDetachedControllerCleanup() {
        var controller = new TestTerminalController();
        var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Terminal.RecordControllerCleanup(controller, cleanup.Task);
        var close = ((IProviderDetailLifetime)Terminal).CloseAsync(CancellationToken.None);

        Assert.That(close.IsCompleted, Is.False);
        cleanup.SetResult();
        await close;

        Assert.That(controller.CloseCalls, Is.EqualTo(0));
    }

    [Test]
    public async Task FileSystemTerminal_WaitForCleanupSharesDetachedStopRetry() {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var controller = new TestTerminalController { CloseCompletion = completion.Task };
        Terminal.RecordControllerCleanup(controller, Task.FromException(new InvalidOperationException("Stop failed.")));

        var first = Terminal.WaitForCleanup(CancellationToken.None);
        var second = Terminal.WaitForCleanup(CancellationToken.None);
        var callsBeforeCompletion = controller.CloseCalls;
        completion.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));

        var laterCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Terminal.RecordControllerCleanup(controller, laterCleanup.Task);
        var laterWait = Terminal.WaitForCleanup(CancellationToken.None);
        var laterWaitIsPending = laterWait.IsCompleted == false;
        laterCleanup.SetResult();
        await laterWait.WaitAsync(TimeSpan.FromSeconds(5));

        using (Assert.EnterMultipleScope()) {
            Assert.That(callsBeforeCompletion, Is.EqualTo(1));
            Assert.That(controller.CloseCalls, Is.EqualTo(1));
            Assert.That(laterWaitIsPending, Is.True);
        }
    }

    [Test]
    public async Task FileSystemTerminal_LifetimeCloseRetriesFailedDetachedCleanup() {
        var controller = new TestTerminalController {
            CloseFailureCalls = new HashSet<int> { 1 }
        };
        var cleanup = Task.FromException(new InvalidOperationException("First stop failed."));
        Terminal.RecordControllerCleanup(controller, cleanup);
        var lifetime = (IProviderDetailLifetime)Terminal;

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await lifetime.CloseAsync(CancellationToken.None));
        await lifetime.CloseAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(controller.CloseCalls, Is.EqualTo(2));
            Assert.That(Terminal.LifetimeToken.IsCancellationRequested, Is.True);
        }
    }

    [Test]
    public void TerminalRelease_TriggeredWork_AllowsPaletteIntentAndFocusesEntries() {
        var controller = new TestTerminalController();
        ((IControlled<IFileSystemTerminalController>)Terminal).Controller = controller;
        Provider.Detail.ShowAsync(FileSystemTerminal.Key, Terminal, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var context = new TestCommandContext {
            Panels = Panels,
            Line = new TestCommandLine {
                Input = "terminal-release"
            }
        };

        Assert.That(TriggeredWork(new TerminalRelease(), context), Is.True);
        Assert.That(controller.FocusEntriesCalls, Is.EqualTo(1));
    }

    [Test]
    public void TerminalRelease_TriggeredWork_RejectsShortcutWhenTerminalDoesNotOwnFocus() {
        var controller = new TestTerminalController();
        ((IControlled<IFileSystemTerminalController>)Terminal).Controller = controller;
        Provider.Detail.ShowAsync(FileSystemTerminal.Key, Terminal, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var context = new TestCommandContext {
            Panels = Panels,
            Gesture = Mock.Of<IGesture>(),
            Line = new TestCommandLine {
                Input = "terminal-release"
            }
        };

        Assert.That(TriggeredWork(new TerminalRelease(), context), Is.False);
        Assert.That(controller.FocusEntriesCalls, Is.EqualTo(0));
    }

    [Test]
    public void TerminalRelease_TriggeredWork_AllowsShortcutWhenTerminalOwnsFocus() {
        var controller = new TestTerminalController {
            HasKeyboardFocusValue = true
        };
        ((IControlled<IFileSystemTerminalController>)Terminal).Controller = controller;
        Provider.Detail.ShowAsync(FileSystemTerminal.Key, Terminal, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var context = new TestCommandContext {
            Panels = Panels,
            Gesture = Mock.Of<IGesture>(),
            Line = new TestCommandLine {
                Input = "terminal-release"
            }
        };

        Assert.That(TriggeredWork(new TerminalRelease(), context), Is.True);
        Assert.That(controller.FocusEntriesCalls, Is.EqualTo(1));
    }

    [Test]
    public void TerminalRelease_TriggeredWork_ReturnsFocusAfterFailedCloseRetainsInvalidatedTerminal() {
        var controller = new TestTerminalController {
            CloseFailureCalls = new HashSet<int> { 1 }
        };
        ((IControlled<IFileSystemTerminalController>)Terminal).Controller = controller;
        Provider.Detail.ShowAsync(FileSystemTerminal.Key, Terminal, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var closed = Provider.Detail.CloseAsync(Terminal, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var context = new TestCommandContext {
            Panels = Panels,
            Line = new TestCommandLine {
                Input = "terminal-release"
            }
        };

        using (Assert.EnterMultipleScope()) {
            Assert.That(closed, Is.False);
            Assert.That(Provider.Detail.Active, Is.SameAs(Terminal));
            Assert.That(Provider.Detail.Error, Is.Not.Null);
            Assert.That(Terminal.IsInvalidated, Is.True);
            Assert.That(TriggeredWork(new TerminalRelease(), context), Is.True);
            Assert.That(controller.FocusEntriesCalls, Is.EqualTo(1));
        }
    }

    [Test]
    public void TerminalOpen_TriggeredWork_OpensEmptyProviderAndReusesCurrentTerminal() {
        var controller = new TestTerminalController();
        ((IControlled<IProviderDetailSetController>)Provider.Detail).Controller =
            new TestProviderDetailSetController(Provider, controller);
        var context = new TestCommandContext {
            Panels = Panels,
            Line = new TestCommandLine {
                Input = "terminal-open"
            }
        };
        var command = new TerminalOpen {
            CommandCliProvider = new CommandCliProvider()
        };

        using (Assert.EnterMultipleScope()) {
            Assert.That(Provider.Provided, Is.Empty);
            Assert.That(((ICommand)command).TriggeredWork(context), Is.True);
            var terminal = Provider.Detail.Active;
            Assert.That(terminal, Is.TypeOf<FileSystemTerminal>());
            Assert.That(((ICommand)command).TriggeredWork(context), Is.True);
            Assert.That(Provider.Detail.Active, Is.SameAs(terminal));
            Assert.That(((FileSystemTerminal)terminal).RequestGeneration, Is.EqualTo(2));
            Assert.That(controller.OpenCalls, Is.EqualTo(2));
            Assert.That(controller.FocusLaterCalls, Is.EqualTo(2));
        }
    }

    [Test]
    public void TerminalClose_TriggeredWork_IsIdempotentAfterClosingExpectedDetail() {
        var controller = new TestTerminalController();
        ((IControlled<IFileSystemTerminalController>)Terminal).Controller = controller;
        Provider.Detail.ShowAsync(FileSystemTerminal.Key, Terminal, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var context = new TestCommandContext {
            Panels = Panels,
            Line = new TestCommandLine {
                Input = "terminal-close"
            }
        };
        var command = new TerminalClose {
            CommandCliProvider = new CommandCliProvider()
        };

        using (Assert.EnterMultipleScope()) {
            Assert.That(((ICommand)command).TriggeredWork(context), Is.True);
            Assert.That(((ICommand)command).TriggeredWork(context), Is.True);
            Assert.That(Provider.Detail.Active, Is.Null);
            Assert.That(controller.CloseCalls, Is.EqualTo(1));
        }
    }

    private static bool TriggeredWork(ICommand command, TestCommandContext context) {
        ((Command)command).CommandCliProvider = new CommandCliProvider();
        return command.TriggeredWork(context);
    }

    private sealed class TestTerminalController : IFileSystemTerminalController {
        public int FocusEntriesCalls { get; private set; }
        public int CloseCalls { get; private set; }
        public int FocusLaterCalls { get; private set; }
        public int OpenCalls { get; private set; }
        public IReadOnlySet<int> CloseFailureCalls { get; init; } = new HashSet<int>();
        public Task CloseCompletion { get; init; } = Task.CompletedTask;
        public bool HasKeyboardFocusValue { get; set; }
        public bool HasKeyboardFocus => HasKeyboardFocusValue;

        public bool RequestFocus() {
            return false;
        }

        public void RequestFocusLater(long generation, long requestGeneration, CancellationToken token) {
            FocusLaterCalls++;
        }

        public Task<bool> OpenAsync(CancellationToken token) {
            OpenCalls++;
            return Task.FromResult(true);
        }

        public Task<bool> FocusEntriesAsync(long generation,
                                            long releaseIntentGeneration,
                                            CancellationToken token) {
            FocusEntriesCalls++;
            return Task.FromResult(true);
        }

        public Task CloseAsync(CancellationToken token) {
            CloseCalls++;
            if (CloseFailureCalls.Contains(CloseCalls)) {
                return Task.FromException(new InvalidOperationException("Controller stop failed."));
            }
            return CloseCompletion;
        }

        public event EventHandler Loaded {
            add {
            }
            remove {
            }
        }
    }

    private sealed class TestProviderDetailSetController : IProviderDetailSetController {
        private readonly TestTerminalController TerminalController;
        private readonly FileSystemProvider Provider;

        public TestProviderDetailSetController(FileSystemProvider provider,
                                               TestTerminalController terminalController) {
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            TerminalController = terminalController ?? throw new ArgumentNullException(nameof(terminalController));
        }

        public async Task InvokeAsync(Func<Task> work) {
            await work();
            var terminal = Provider.Detail.Active as FileSystemTerminal;
            var terminalNeedsController = terminal is not null && terminal.CurrentController is null;
            if (terminalNeedsController) {
                ((IControlled<IFileSystemTerminalController>)terminal).Controller = TerminalController;
            }
        }

        public event EventHandler Loaded {
            add {
            }
            remove {
            }
        }
    }
}
