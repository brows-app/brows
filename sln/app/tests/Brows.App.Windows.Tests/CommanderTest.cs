using Brows.Commands;
using Brows.Entries;
using Brows.Gui;
using Brows.Panels;
using Brows.Providers;
using Brows.Triggers;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
internal sealed class CommanderTest {
    private static readonly IGesture Gesture = new PressGesture(PressKey.F1);

    private static Mock<ICommanderController> AttachController(Commander commander) {
        var controller = new Mock<ICommanderController>();
        ((IControlled<ICommanderController>)commander).Controller = controller.Object;
        return controller;
    }

    private static Commander CreateCommander(IEnumerable<ICommand> commands, params IProvider[] providers) {
        var factory = new Mock<IProviderFactory>();
        factory.Setup(item => item.CreateFor(
                It.IsAny<string>(),
                It.IsAny<IPanel>(),
                It.IsAny<CancellationToken>()))
            .Returns((string id, IPanel panel, CancellationToken token) =>
                Task.FromResult(providers.FirstOrDefault(provider => provider.ID == id)));
        return new Commander(
            new ProviderFactorySet([factory.Object]),
            messages: null,
            new CommandCollection(commands),
            domain: null);
    }

    private static Mock<ICommand> CreateCommand(Func<ICommandContext, bool> work) {
        var gestureTrigger = new Mock<IGestureTrigger>();
        gestureTrigger.SetupGet(item => item.Defined).Returns("*");
        var gestures = new Mock<IGestureTriggerCollection>();
        gestures.Setup(item => item[Gesture]).Returns(gestureTrigger.Object);

        var trigger = new Mock<ICommandTrigger>();
        trigger.SetupGet(item => item.Gestures).Returns(gestures.Object);
        var configuredGesture = gestureTrigger.Object;
        trigger.Setup(item => item.Triggered(Gesture, out configuredGesture)).Returns(true);

        var command = new Mock<ICommand>();
        command.SetupGet(item => item.Trigger).Returns(trigger.Object);
        command.Setup(item => item.TriggeredWork(It.IsAny<ICommandContext>()))
            .Returns((ICommandContext context) => work(context));
        return command;
    }

    private static bool ContextHasProvider(ICommandContext context, IProvider expectedProvider) {
        var hasPanel = context.HasPanel(out var panel);
        if (hasPanel == false) {
            return false;
        }
        var hasProvider = panel.HasProvider(out var activeProvider);
        var isExpectedProvider = hasProvider && ReferenceEquals(expectedProvider, activeProvider);
        return isExpectedProvider;
    }

    private static ProviderFixture CreateProvider(string id) {
        var observation = new Mock<IEntryObservation>();
        observation.SetupGet(item => item.Observed).Returns(Array.Empty<IEntry>());
        observation.Setup(item => item.Focus()).Returns(false);

        var provider = new Mock<IEntryProvider>();
        provider.SetupGet(item => item.ID).Returns(id);
        provider.SetupGet(item => item.Observation).Returns(observation.Object);
        provider.Setup(item => item.Focus()).Returns(() => observation.Object.Focus());
        return new ProviderFixture(provider.Object, observation);
    }

    private static Mock<IProvider> CreateProviderWithoutEntries(string id) {
        var provider = new Mock<IProvider>();
        provider.SetupGet(item => item.ID).Returns(id);
        provider.Setup(item => item.Focus()).Returns(true);
        return provider;
    }

    private static async Task AddPanel(Commander commander, string id) {
        var added = await commander.AddPanel(id, CancellationToken.None);
        Assert.That(added, Is.True);
    }

    private static GestureEventArgs RaiseGesture(
        Mock<ICommanderController> controller,
        Commander commander,
        object source) {
        var args = new GestureEventArgs(Gesture, source);
        controller.Raise(item => item.WindowGesture += null, commander, args);
        return args;
    }

    private static InputEventArgs RaiseText(
        Mock<ICommanderController> controller,
        Commander commander,
        object source) {
        var args = new InputEventArgs("x", source);
        controller.Raise(item => item.WindowInput += null, commander, args);
        return args;
    }

    [Test]
    public async Task Input_ScopedGestureFiltersDisallowedCommandsEvenAfterAllowedCommandDeclines() {
        var provider = CreateProvider("first");
        var allowedCommand = CreateCommand(_ => false);
        var disallowedCommand = CreateCommand(_ => true);
        var commander = CreateCommander(
            [allowedCommand.Object, disallowedCommand.Object],
            provider.Provider);
        await AddPanel(commander, "first");
        var controller = AttachController(commander);
        var source = new ScopedSource(provider.Provider, command => command == allowedCommand.Object);

        var args = RaiseGesture(controller, commander, source);

        Assert.That(args.Triggered, Is.False);
        allowedCommand.Verify(item => item.TriggeredWork(It.IsAny<ICommandContext>()), Times.Once);
        disallowedCommand.Verify(item => item.TriggeredWork(It.IsAny<ICommandContext>()), Times.Never);
        provider.Observation.Verify(item => item.Focus(), Times.Once);
    }

    [Test]
    public async Task Input_ScopedGestureRunsAllowedCommandWithoutActivatingPanel() {
        var provider = CreateProvider("first");
        var command = CreateCommand(context => ContextHasProvider(context, provider.Provider));
        var commander = CreateCommander([command.Object], provider.Provider);
        await AddPanel(commander, "first");
        var controller = AttachController(commander);
        var source = new ScopedSource(provider.Provider, _ => true);

        var args = RaiseGesture(controller, commander, source);

        Assert.That(args.Triggered, Is.True);
        command.Verify(item => item.TriggeredWork(It.IsAny<ICommandContext>()), Times.Once);
        provider.Observation.Verify(item => item.Focus(), Times.Once);
    }

    [TestCase("stale", TestName = "Input_ScopedGestureWithWrongProviderAndDifferentIdDoesNotDispatch")]
    [TestCase("active", TestName = "Input_ScopedGestureWithWrongProviderAndSameIdDoesNotDispatch")]
    public async Task Input_ScopedGestureWithWrongProviderDoesNotDispatch(string scopedProviderId) {
        var activeProvider = CreateProvider("active");
        var staleProvider = CreateProvider(scopedProviderId);
        var command = CreateCommand(_ => true);
        var commander = CreateCommander([command.Object], activeProvider.Provider, staleProvider.Provider);
        await AddPanel(commander, "active");
        var controller = AttachController(commander);
        var source = new ScopedSource(staleProvider.Provider, _ => true);

        var args = RaiseGesture(controller, commander, source);

        Assert.That(args.Triggered, Is.False);
        command.Verify(item => item.TriggeredWork(It.IsAny<ICommandContext>()), Times.Never);
        activeProvider.Observation.Verify(item => item.Focus(), Times.Once);
    }

    [Test]
    public async Task Input_ScopedGestureWithoutActivePanelDoesNotFallBackToFirstPanel() {
        var provider = CreateProvider("first");
        var command = CreateCommand(_ => true);
        var commander = CreateCommander([command.Object], provider.Provider);
        await AddPanel(commander, "first");
        var panels = (PanelCollection)commander.Panels;
        typeof(PanelCollection).GetProperty(nameof(PanelCollection.Active)).SetValue(panels, null);
        var controller = AttachController(commander);
        var source = new ScopedSource(provider.Provider, _ => true);

        var args = RaiseGesture(controller, commander, source);

        Assert.That(commander.Panels.Count, Is.EqualTo(1));
        Assert.That(commander.Panels.Active, Is.Null);
        Assert.That(args.Triggered, Is.False);
        command.Verify(item => item.TriggeredWork(It.IsAny<ICommandContext>()), Times.Never);
        provider.Observation.Verify(item => item.Focus(), Times.Once);
    }

    [Test]
    public async Task Input_ScopedGestureRejectsPanelRetargetedByPolicy() {
        var firstProvider = CreateProvider("first");
        var secondProvider = CreateProvider("second");
        Commander commander = null;
        var source = new ScopedSource(
            secondProvider.Provider,
            _ => true,
            onAllows: () => ((PanelCollection)commander.Panels)[0].Activate());
        var command = CreateCommand(_ => true);
        commander = CreateCommander([command.Object], firstProvider.Provider, secondProvider.Provider);
        await AddPanel(commander, "first");
        await AddPanel(commander, "second");
        var controller = AttachController(commander);

        var args = RaiseGesture(controller, commander, source);

        Assert.That(args.Triggered, Is.False);
        Assert.That(source.AllowsCalls, Is.EqualTo(1));
        Assert.That(source.ProviderReads, Is.EqualTo(1));
        command.Verify(item => item.TriggeredWork(It.IsAny<ICommandContext>()), Times.Never);
        Assert.That(commander.Panels.Active.HasProvider(out var activeProvider), Is.True);
        Assert.That(activeProvider, Is.SameAs(firstProvider.Provider));
    }

    [Test]
    public async Task Input_ScopedGestureRevalidatesCapturedProviderAfterADecliningCommand() {
        var firstProvider = CreateProvider("first");
        var secondProvider = CreateProvider("second");
        var commandCalls = 0;
        Commander commander = null;
        var source = new ScopedSource(secondProvider.Provider, _ => true);
        var work = new Func<ICommandContext, bool>(_ => {
            commandCalls++;
            source.Retarget(firstProvider.Provider);
            ((PanelCollection)commander.Panels)[0].Activate();
            return false;
        });
        var firstCommand = CreateCommand(work);
        var secondCommand = CreateCommand(work);
        commander = CreateCommander(
            [firstCommand.Object, secondCommand.Object],
            firstProvider.Provider,
            secondProvider.Provider);
        await AddPanel(commander, "first");
        await AddPanel(commander, "second");
        var controller = AttachController(commander);

        var args = RaiseGesture(controller, commander, source);

        Assert.That(args.Triggered, Is.False);
        Assert.That(commandCalls, Is.EqualTo(1));
        Assert.That(source.ProviderReads, Is.EqualTo(1));
    }

    [Test]
    public async Task Input_OrdinaryUnmatchedGestureActivatesActivePanel() {
        var provider = CreateProvider("first");
        var commander = CreateCommander([], provider.Provider);
        await AddPanel(commander, "first");
        var controller = AttachController(commander);

        var args = RaiseGesture(controller, commander, source: null);

        Assert.That(args.Triggered, Is.False);
        provider.Observation.Verify(item => item.Focus(), Times.Exactly(2));
    }

    [Test]
    public async Task Input_MarkerOnlyGestureSuppressesCommands() {
        var provider = CreateProvider("first");
        var command = CreateCommand(_ => true);
        var commander = CreateCommander([command.Object], provider.Provider);
        await AddPanel(commander, "first");
        var controller = AttachController(commander);

        var args = RaiseGesture(controller, commander, new GestureMarker());

        Assert.That(args.Triggered, Is.False);
        command.Verify(item => item.TriggeredWork(It.IsAny<ICommandContext>()), Times.Never);
        provider.Observation.Verify(item => item.Focus(), Times.Once);
    }

    [Test]
    public async Task Input_TextMarkerSuppressesPanelText() {
        var provider = CreateProvider("first");
        var commander = CreateCommander([], provider.Provider);
        await AddPanel(commander, "first");
        var controller = AttachController(commander);
        provider.Observation.Invocations.Clear();

        var args = RaiseText(controller, commander, new TextMarker());

        Assert.That(args.Triggered, Is.False);
        provider.Observation.VerifyGet(item => item.Observed, Times.Never);
    }

    [Test]
    public async Task AddPanel_ProviderWithoutEntriesActivatesPanelThroughProvider() {
        var provider = CreateProviderWithoutEntries("view");
        var commander = CreateCommander([], provider.Object);

        await AddPanel(commander, "view");

        var panel = (Panel)commander.Panels[0];
        Assert.That(panel.Activated, Is.True);
        Assert.That(panel.State.ItemsObserved, Is.False);
        provider.Verify(item => item.Focus(), Times.Once);
    }

    [Test]
    public async Task AddPanel_ProviderWithEntriesShowsItemCounts() {
        var provider = CreateProvider("first");
        var commander = CreateCommander([], provider.Provider);

        await AddPanel(commander, "first");

        Assert.That(((Panel)commander.Panels[0]).State.ItemsObserved, Is.True);
    }

    [Test]
    public async Task ProviderControllerChanged_ActivatesActivePanel() {
        var provider = CreateProviderWithoutEntries("view");
        var commander = CreateCommander([], provider.Object);
        await AddPanel(commander, "view");

        provider.Raise(item => item.ControllerChanged += null, provider.Object, EventArgs.Empty);

        provider.Verify(item => item.Focus(), Times.Exactly(2));
    }

    [Test]
    public async Task ProviderControllerChanged_AfterProviderIsReplacedDoesNotActivatePanel() {
        var provider = CreateProviderWithoutEntries("view");
        var replacement = CreateProviderWithoutEntries("other");
        var commander = CreateCommander([], provider.Object, replacement.Object);
        await AddPanel(commander, "view");
        await commander.Panels[0].Provide("other", CancellationToken.None);

        provider.Raise(item => item.ControllerChanged += null, provider.Object, EventArgs.Empty);

        provider.Verify(item => item.Focus(), Times.Once);
    }

    [Test]
    public async Task Input_TextInPanelWithoutEntriesIsNotConsumed() {
        var provider = CreateProviderWithoutEntries("view");
        var commander = CreateCommander([], provider.Object);
        await AddPanel(commander, "view");
        var controller = AttachController(commander);

        var args = RaiseText(controller, commander, source: null);

        Assert.That(args.Triggered, Is.False);
    }

    private sealed record ProviderFixture(IProvider Provider, Mock<IEntryObservation> Observation);

    private sealed class ScopedSource : IControllingCommandGesture, IControllingText {
        private IProvider CurrentProvider;
        private readonly Func<ICommand, bool> AllowCommand;
        private readonly Action OnAllows;

        public IProvider Provider {
            get {
                ProviderReads++;
                return CurrentProvider;
            }
        }

        public int ProviderReads { get; private set; }

        public int AllowsCalls { get; private set; }

        public ScopedSource(IProvider provider, Func<ICommand, bool> allowCommand, Action onAllows = null) {
            CurrentProvider = provider;
            AllowCommand = allowCommand;
            OnAllows = onAllows;
        }

        public bool Allows(ICommand command) {
            AllowsCalls++;
            OnAllows?.Invoke();
            return AllowCommand(command);
        }

        public void Retarget(IProvider provider) {
            CurrentProvider = provider;
        }
    }

    [Test]
    public async Task DisposeAsync_AwaitsProviderCleanupBeforeRemovingPanel() {
        var provider = new Mock<IEntryProvider>();
        provider.SetupGet(item => item.ID).Returns("one");
        var observation = new Mock<IEntryObservation>();
        observation.SetupGet(item => item.Observed).Returns(Array.Empty<IEntry>());
        provider.SetupGet(item => item.Observation).Returns(observation.Object);
        var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.As<IAsyncDisposable>().Setup(item => item.DisposeAsync())
            .Returns(() => new ValueTask(cleanup.Task));
        var commander = CreateCommander([], provider.Object);
        await AddPanel(commander, "one");
        var first = ((IAsyncDisposable)commander).DisposeAsync().AsTask();
        var second = ((IAsyncDisposable)commander).DisposeAsync().AsTask();
        Assert.That(second, Is.SameAs(first));
        Assert.That(first.IsCompleted, Is.False);
        Assert.That(commander.Panels.Count, Is.EqualTo(1));
        cleanup.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(commander.Panels.Count, Is.Zero);
        provider.Verify(item => item.End(), Times.Once);
        provider.As<IAsyncDisposable>().Verify(item => item.DisposeAsync(), Times.Once);
    }

    [Test]
    public async Task Provide_AwaitsPreviousProviderCleanupBeforeInstallingReplacement() {
        var first = new Mock<IEntryProvider>();
        first.SetupGet(item => item.ID).Returns("one");
        var observation = new Mock<IEntryObservation>();
        observation.SetupGet(item => item.Observed).Returns(Array.Empty<IEntry>());
        first.SetupGet(item => item.Observation).Returns(observation.Object);
        var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        first.As<IAsyncDisposable>().Setup(item => item.DisposeAsync())
            .Returns(() => new ValueTask(cleanup.Task));
        var second = CreateProvider("two");
        var commander = CreateCommander([], first.Object, second.Provider);
        await AddPanel(commander, "one");
        var panel = commander.Panels.Active;
        var navigation = panel.Provide("two", CancellationToken.None);
        Assert.That(navigation.IsCompleted, Is.False);
        Assert.That(panel.HasProvider(out IProvider before), Is.True);
        Assert.That(before, Is.SameAs(first.Object));
        Mock.Get(second.Provider).Verify(item => item.Begin(), Times.Never);
        cleanup.SetResult();
        Assert.That(await navigation.WaitAsync(TimeSpan.FromSeconds(5)), Is.True);
        Assert.That(panel.HasProvider(out IProvider after), Is.True);
        Assert.That(after, Is.SameAs(second.Provider));
        Mock.Get(second.Provider).Verify(item => item.Begin(), Times.Once);
        await ((IAsyncDisposable)commander).DisposeAsync();
    }

    [Test]
    public async Task Provide_InstallsReplacementRequestedWithTokenOfStoppedProvider() {
        /*
         * A provider that replaces itself, as an SFTP connection does once it
         * authenticates, requests the replacement with its own token. Stopping
         * that provider cancels the token.
         */
        using var outgoing = new CancellationTokenSource();
        var first = CreateProvider("one");
        Mock.Get(first.Provider).Setup(item => item.End()).Callback(() => outgoing.Cancel());
        var second = CreateProvider("two");
        var commander = CreateCommander([], first.Provider, second.Provider);
        await AddPanel(commander, "one");
        var panel = commander.Panels.Active;

        var provided = await panel.Provide("two", outgoing.Token);

        Assert.That(provided, Is.True);
        Assert.That(outgoing.IsCancellationRequested, Is.True);
        Assert.That(panel.HasProvider(out IProvider after), Is.True);
        Assert.That(after, Is.SameAs(second.Provider));
        Mock.Get(second.Provider).Verify(item => item.Begin(), Times.Once);
        Mock.Get(second.Provider).Verify(item => item.End(), Times.Never);
        await ((IAsyncDisposable)commander).DisposeAsync();
    }

    [Test]
    public async Task DisposeAsync_FailedLaterPanelKeepsRemainingPanelActiveUntilRetry() {
        var first = CreateProvider("first");
        var second = new Mock<IEntryProvider>();
        var observation = new Mock<IEntryObservation>();
        observation.SetupGet(item => item.Observed).Returns(Array.Empty<IEntry>());
        second.SetupGet(item => item.ID).Returns("second");
        second.SetupGet(item => item.Observation).Returns(observation.Object);
        var attempts = 0;
        second.As<IAsyncDisposable>().Setup(item => item.DisposeAsync())
            .Returns(() => {
                attempts++;
                return attempts == 1
                    ? ValueTask.FromException(new InvalidOperationException("close failed"))
                    : ValueTask.CompletedTask;
            });
        var commander = CreateCommander([], first.Provider, second.Object);
        await AddPanel(commander, "first");
        await AddPanel(commander, "second");
        ((Panel)commander.Panels[0]).Activate();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await ((IAsyncDisposable)commander).DisposeAsync();
        });
        Assert.That(commander.Panels.Count, Is.EqualTo(1));
        Assert.That(commander.Panels.Active, Is.SameAs(commander.Panels[0]));
        Assert.That(commander.Panels.Passive, Is.Null);
        Assert.That(commander.Panels[0].Column, Is.Zero);
        Assert.That(commander.Panels.Active.HasProvider(out var provider), Is.True);
        Assert.That(provider, Is.SameAs(second.Object));

        await ((IAsyncDisposable)commander).DisposeAsync();
        Assert.That(commander.Panels.Count, Is.Zero);
    }

    [Test]
    public async Task RemovePanel_CompletesWhileUnrelatedProviderCreationIsPending() {
        var first = CreateProvider("first");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<IProvider>(TaskCreationOptions.RunContinuationsAsynchronously);
        var factory = new Mock<IProviderFactory>();
        factory.Setup(item => item.CreateFor(It.IsAny<string>(), It.IsAny<IPanel>(), It.IsAny<CancellationToken>()))
            .Returns((string id, IPanel _, CancellationToken _) => {
                if (id == "first") {
                    return Task.FromResult(first.Provider);
                }
                entered.TrySetResult();
                return release.Task;
            });
        var commander = new Commander(new ProviderFactorySet([factory.Object]), null, new CommandCollection([]), null);
        await AddPanel(commander, "first");
        var pending = commander.AddPanel("second", CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try {
            Assert.That(await commander.RemovePanel(commander.Panels[0], CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5)), Is.True);
        }
        finally {
            release.TrySetResult(null);
        }
        Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(5)), Is.False);
        await ((IAsyncDisposable)commander).DisposeAsync();
    }

    [Test]
    public async Task RemovePanel_ActivatesPanelAtRemovedColumnAndCompactsColumns() {
        var first = CreateProvider("first");
        var second = CreateProvider("second");
        var third = CreateProvider("third");
        var commander = CreateCommander([], first.Provider, second.Provider, third.Provider);
        await AddPanel(commander, "first");
        await AddPanel(commander, "second");
        await AddPanel(commander, "third");
        ((Panel)commander.Panels[0]).Activate();

        Assert.That(await commander.RemovePanel(commander.Panels[0], CancellationToken.None), Is.True);
        Assert.That(commander.Panels.Active, Is.SameAs(commander.Panels[0]));
        Assert.That(commander.Panels.Active.HasProvider(out var provider), Is.True);
        Assert.That(provider, Is.SameAs(second.Provider));
        Assert.That(commander.Panels[0].Column, Is.Zero);
        Assert.That(commander.Panels[1].Column, Is.EqualTo(1));
        Assert.That(commander.Panels.Passive, Is.Null.Or.SameAs(commander.Panels[1]));
        await ((IAsyncDisposable)commander).DisposeAsync();
    }

    [Test]
    public async Task AddPanel_ConcurrentOpenAssignsDistinctColumnsWhenCompletedOutOfOrder() {
        var first = CreateProvider("first");
        var second = CreateProvider("second");
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRelease = new TaskCompletionSource<IProvider>(TaskCreationOptions.RunContinuationsAsynchronously);
        var factory = new Mock<IProviderFactory>();
        factory.Setup(item => item.CreateFor(It.IsAny<string>(), It.IsAny<IPanel>(), It.IsAny<CancellationToken>()))
            .Returns((string id, IPanel _, CancellationToken _) => {
                if (id == "first") {
                    firstEntered.TrySetResult();
                    return firstRelease.Task;
                }
                return Task.FromResult(second.Provider);
            });
        var commander = new Commander(new ProviderFactorySet([factory.Object]), null, new CommandCollection([]), null);
        var firstAdd = commander.AddPanel("first", CancellationToken.None);
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try {
            var secondAdded = await commander.AddPanel("second", CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(secondAdded, Is.True);
        }
        finally {
            firstRelease.TrySetResult(first.Provider);
        }
        Assert.That(await firstAdd.WaitAsync(TimeSpan.FromSeconds(5)), Is.True);
        Assert.That(new[] { commander.Panels[0].Column, commander.Panels[1].Column },
            Is.EquivalentTo(new[] { 0, 1 }));
        await ((IAsyncDisposable)commander).DisposeAsync();
    }

    [Test]
    public async Task DisposeAsync_JoinsPendingPanelCreationAndRejectsAdmission() {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<IProvider>(TaskCreationOptions.RunContinuationsAsynchronously);
        var candidate = CreateProvider("pending");
        var factory = new Mock<IProviderFactory>();
        factory.Setup(item => item.CreateFor(It.IsAny<string>(), It.IsAny<IPanel>(), It.IsAny<CancellationToken>()))
            .Returns((string _, IPanel _, CancellationToken token) => {
                token.Register(() => canceled.TrySetResult());
                entered.TrySetResult();
                return release.Task;
            });
        var commander = new Commander(new ProviderFactorySet([factory.Object]), null, new CommandCollection([]), null);
        var adding = commander.AddPanel("pending", CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var clearing = ((IAsyncDisposable)commander).DisposeAsync().AsTask();
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(clearing.IsCompleted, Is.False);
        Assert.That(await commander.AddPanel("later", CancellationToken.None), Is.False);

        release.SetResult(candidate.Provider);
        Assert.That(async () => await adding.WaitAsync(TimeSpan.FromSeconds(5)),
            Throws.InstanceOf<OperationCanceledException>());
        await clearing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(commander.Panels.Count, Is.Zero);
        Mock.Get(candidate.Provider).Verify(item => item.Begin(), Times.Never);
        Mock.Get(candidate.Provider).Verify(item => item.Dispose(), Times.Once);
    }

    [Test]
    public async Task DisposeAsync_RetriesFailedUnadmittedPanelCleanup() {
        var active = new Mock<IEntryProvider>();
        var activeObservation = new Mock<IEntryObservation>();
        activeObservation.SetupGet(item => item.Observed).Returns(Array.Empty<IEntry>());
        active.SetupGet(item => item.ID).Returns("active");
        active.SetupGet(item => item.Observation).Returns(activeObservation.Object);
        var removeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var removeRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        active.As<IAsyncDisposable>().Setup(item => item.DisposeAsync()).Returns(() => {
            removeEntered.TrySetResult();
            return new ValueTask(removeRelease.Task);
        });
        var pending = new Mock<IEntryProvider>();
        var pendingObservation = new Mock<IEntryObservation>();
        pendingObservation.SetupGet(item => item.Observed).Returns(Array.Empty<IEntry>());
        pending.SetupGet(item => item.ID).Returns("pending");
        pending.SetupGet(item => item.Observation).Returns(pendingObservation.Object);
        var cleanupAttempts = 0;
        pending.As<IAsyncDisposable>().Setup(item => item.DisposeAsync()).Returns(() => {
            cleanupAttempts++;
            return cleanupAttempts < 3
                ? ValueTask.FromException(new InvalidOperationException("cleanup failed"))
                : ValueTask.CompletedTask;
        });
        var pendingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var factory = new Mock<IProviderFactory>();
        factory.Setup(item => item.CreateFor(It.IsAny<string>(), It.IsAny<IPanel>(), It.IsAny<CancellationToken>()))
            .Returns((string id, IPanel _, CancellationToken token) => {
                if (id == "active") {
                    return Task.FromResult<IProvider>(active.Object);
                }
                pendingStarted.TrySetResult();
                return Task.FromResult<IProvider>(pending.Object);
            });
        var commander = new Commander(new ProviderFactorySet([factory.Object]), null, new CommandCollection([]), null);
        var panels = new PanelCollection(commander.Providers, commander);
        var activePanel = await panels.Add("active", CancellationToken.None);
        var removing = panels.Remove(activePanel);
        await removeEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try {
            var adding = panels.Add("pending", CancellationToken.None);
            await pendingStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var firstClear = panels.ClearAsync();
            removeRelease.SetResult();
            Assert.That(await removing.WaitAsync(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(async () => await adding.WaitAsync(TimeSpan.FromSeconds(5)),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(async () => await firstClear.WaitAsync(TimeSpan.FromSeconds(5)),
                Throws.TypeOf<InvalidOperationException>());
            await panels.ClearAsync();
            Assert.That(cleanupAttempts, Is.EqualTo(3));
            Assert.That(panels.Count, Is.Zero);
        }
        finally {
            removeRelease.TrySetResult();
        }
    }

    [Test]
    public async Task ClearAsync_JoinsProspectiveProviderCleanupAfterInitialDisposalFailures() {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<IProvider>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thirdDisposeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thirdDisposeRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var prospective = new Mock<IProvider>();
        var attempts = 0;
        prospective.As<IAsyncDisposable>().Setup(item => item.DisposeAsync()).Returns(() => {
            attempts++;
            if (attempts < 3) {
                return ValueTask.FromException(new InvalidOperationException("cleanup failed"));
            }
            thirdDisposeEntered.TrySetResult();
            return new ValueTask(thirdDisposeRelease.Task);
        });
        var factory = new Mock<IProviderFactory>();
        factory.Setup(item => item.CreateFor(It.IsAny<string>(), It.IsAny<IPanel>(), It.IsAny<CancellationToken>()))
            .Returns((string _, IPanel _, CancellationToken _) => {
                entered.TrySetResult();
                return release.Task;
            });
        var commander = new Commander(new ProviderFactorySet([factory.Object]), null, new CommandCollection([]), null);
        var panels = new PanelCollection(commander.Providers, commander);
        var adding = panels.Add("prospective", CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var clearing = panels.ClearAsync();
        try {
            release.SetResult(prospective.Object);
            await thirdDisposeEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(clearing.IsCompleted, Is.False);
            Assert.That(async () => await adding.WaitAsync(TimeSpan.FromSeconds(5)),
                Throws.TypeOf<InvalidOperationException>());
        }
        finally {
            release.TrySetResult(prospective.Object);
            thirdDisposeRelease.TrySetResult();
        }
        await clearing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(attempts, Is.EqualTo(3));
        Assert.That(panels.Count, Is.Zero);
    }

    [Test]
    public async Task Provide_RetiresFailedProspectiveProviderBeforeCreatingReplacement() {
        var prospective = new Mock<IProvider>();
        var disposals = 0;
        prospective.As<IAsyncDisposable>().Setup(item => item.DisposeAsync()).Returns(() => {
            disposals++;
            return disposals == 1
                ? ValueTask.FromException(new InvalidOperationException("cleanup failed"))
                : ValueTask.CompletedTask;
        });
        var replacement = CreateProvider("replacement");
        using var canceled = new CancellationTokenSource();
        var factory = new Mock<IProviderFactory>();
        var replacementCreated = false;
        factory.Setup(item => item.CreateFor(It.IsAny<string>(), It.IsAny<IPanel>(), It.IsAny<CancellationToken>()))
            .Returns((string id, IPanel _, CancellationToken _) => {
                if (id == "prospective") {
                    canceled.Cancel();
                    return Task.FromResult(prospective.Object);
                }
                Assert.That(disposals, Is.EqualTo(2));
                replacementCreated = true;
                return Task.FromResult(replacement.Provider);
            });
        var commander = new Commander(new ProviderFactorySet([factory.Object]), null, new CommandCollection([]), null);
        var panel = new Panel(commander.Providers, commander);

        Assert.That(async () => await panel.Provide("prospective", canceled.Token),
            Throws.TypeOf<InvalidOperationException>());
        Assert.That(await panel.Provide("replacement", CancellationToken.None), Is.True);
        Assert.That(replacementCreated, Is.True);
        Assert.That(disposals, Is.EqualTo(2));
        await panel.CloseAsync();
    }
    private sealed class GestureMarker : IControllingGesture {
    }

    private sealed class TextMarker : IControllingText {
    }
}
