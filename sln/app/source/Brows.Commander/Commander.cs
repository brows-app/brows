using Brows.Commands;
using Brows.Composition;
using Brows.Gui;
using Brows.Operations;
using Brows.Panels;
using Brows.Providers;
using Brows.Sys;
using Brows.Triggers;
using Domore.Logs;
using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed class Commander : Notifier, ICommander, IControlled<ICommanderController>, IAsyncDisposable {
    private static readonly ILog Log = Logging.For(typeof(Commander));

    private readonly Lock DisposalLocker = new();
    private Task DisposalTask;
    private volatile bool Disposing;
    private CancellationTokenSource SystemMessageCancellation;

    private PanelCollection PanelCollection => field ??=
        new PanelCollection(Providers, this);

    private async void Controller_Loaded(object sender, EventArgs e) {
        var token = CancellationToken.None;
        Loaded?.Invoke(this, e);

        ReadSystemMessages();

        var load = Load.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        foreach (var item in load) {
            await AddPanel(item, token);
        }
        if (First) {
            await ShowPalette(source: null, token: token, config: new CommandPaletteConfig {
                Input = "? ",
                SelectedStart = 0,
                SelectedLength = 2
            });
        }
    }

    private async void Controller_WindowClosed(object sender, EventArgs e) {
        try {
            await ((IAsyncDisposable)this).DisposeAsync();
            Closed?.Invoke(this, e);
        }
        catch (Exception exception) {
            Log.Error(nameof(Controller_WindowClosed), exception);
        }
    }

    private void Controller_WindowClosing(object sender, EventArgs e) {
        CancelSystemMessages();
    }

    private void CancelSystemMessages() {
        try {
            SystemMessageCancellation?.Cancel();
        }
        catch (ObjectDisposedException) {
        }
    }

    private async Task DisposeCoreAsync() {
        await Task.Yield();
        CancelSystemMessages();
        await PanelCollection.ClearAsync();
    }
    private void Controller_WindowInput(object sender, InputEventArgs e) {
        if (e != null) {
            e.Triggered = Input(e.Text, e.Source);
        }
    }

    private void Controller_WindowGesture(object sender, GestureEventArgs e) {
        if (e != null) {
            e.Triggered = Input(e.Gesture, e.Source);
        }
    }

    private async void ReadSystemMessages() {
        var oldCTS = SystemMessageCancellation;
        try {
            oldCTS?.Dispose();
        }
        catch (ObjectDisposedException) {
        }
        var newCTS = SystemMessageCancellation = new();
        using (newCTS) {
            try {
                await ReadSystemMessages(newCTS.Token);
            }
            catch (Exception ex) {
                Log.Error(ex);
            }
        }
    }

    private async Task ReadSystemMessages(CancellationToken cancellationToken) {
        var window = NativeWindow();
        var messages = Messages?.ReadSystemMessages(window, cancellationToken);
        if (messages is null) {
            return;
        }
        await foreach (var message in messages) {
            IEnumerable<Task> tasks() {
                foreach (var panel in PanelCollection) {
                    async Task task() {
                        try {
                            await panel.Post(message, cancellationToken);
                        }
                        catch (Exception ex) {
                            if (Log.Error()) {
                                Log.Error(ex);
                            }
                        }
                    }
                    yield return task();
                }
            }
            await Task.WhenAll(tasks());
        }
    }

    private bool ScopedPanelOwnsProvider(IPanel panel, IProvider provider) {
        var hasNullInput = panel is null || provider is null;
        if (hasNullInput) {
            return false;
        }
        var activePanel = PanelCollection.Active;
        var panelIsActive = ReferenceEquals(panel, activePanel);
        if (panelIsActive == false) {
            return false;
        }
        var panelHasProvider = panel.HasProvider(out var activeProvider);
        var panelOwnsProvider = panelHasProvider && ReferenceEquals(provider, activeProvider);
        return panelOwnsProvider;
    }

    private bool Input(IGesture gesture, object source) {
        if (Palette != null) {
            return false;
        }
        if (source is IControllingCommandGesture scoped) {
            var scopedPanel = (IPanel)PanelCollection.Active;
            var scopedProvider = scoped.Provider;
            var panelIsScopedOwner = ScopedPanelOwnsProvider(scopedPanel, scopedProvider);
            if (panelIsScopedOwner == false) {
                return false;
            }
            var scopedTriggered = Commands.Triggered(gesture, out var scopedItems);
            if (scopedTriggered == false) {
                return false;
            }
            foreach (var item in scopedItems) {
                var command = item.Value;
                if (command is null) {
                    continue;
                }
                var panelStillOwnsProvider = ScopedPanelOwnsProvider(scopedPanel, scopedProvider);
                if (panelStillOwnsProvider == false) {
                    return false;
                }
                var commandIsAllowed = scoped.Allows(command);
                if (commandIsAllowed == false) {
                    continue;
                }
                panelStillOwnsProvider = ScopedPanelOwnsProvider(scopedPanel, scopedProvider);
                if (panelStillOwnsProvider == false) {
                    return false;
                }
                var defined = command?.Trigger?.Gestures?[gesture]?.Defined;
                if (defined is null) {
                    continue;
                }
                panelStillOwnsProvider = ScopedPanelOwnsProvider(scopedPanel, scopedProvider);
                if (panelStillOwnsProvider == false) {
                    return false;
                }
                var cmdLine = new CommandLine(defined, null);
                var context = new CommandContext(this, new CommandSource(source), cmdLine, gesture);
                var work = command.TriggeredWork(context);
                if (work) {
                    return true;
                }
            }
            return false;
        }
        if (source is IControllingGesture) {
            return false;
        }
        var panel = PanelCollection.Active;
        if (panel == null) {
            panel = PanelCollection.Count > 0 ? PanelCollection[0] : null;
        }
        if (panel != null) {
            if (panel.Activated == false) {
                panel.Activate();
            }
        }
        var triggered = Commands.Triggered(gesture, out var items);
        if (triggered) {
            foreach (var item in items) {
                var command = item.Value;
                var defined = command?.Trigger?.Gestures?[gesture]?.Defined;
                if (defined != null) {
                    var cmdLine = new CommandLine(defined, null);
                    var context = new CommandContext(this, new CommandSource(source), cmdLine, gesture);
                    var work = command.TriggeredWork(context);
                    if (work) {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private bool Input(string text, object source) {
        if (Palette != null) {
            return false;
        }
        if (source is IControllingText) {
            return false;
        }
        if (Log.Info()) {
            Log.Info(Log.Join(nameof(Input), text));
        }
        var panel = PanelCollection.Active;
        if (panel == null) {
            return false;
        }
        return panel.Text(text);
    }

    public event EventHandler Loaded;
    public event EventHandler Closed;

    public bool First {
        get;
        set => Change(ref field, value);
    }

    public IReadOnlyList<string> Load {
        get => field ??= [];
        set;
    }

    public CommandPalette Palette {
        get => field;
        private set => Change(ref field, value);
    }

    public bool DialogFocused {
        get;
        private set => Change(ref field, value);
    }

    public IOperator Operator => field ??=
        Import?.Find<IOperatorFactory>()?.Create();

    public IImport Import =>
        Domain?.Import;

    public IPanelCollection Panels =>
        PanelCollection;

    public CommanderDomain Domain { get; }
    public CommandCollection Commands { get; }
    public ProviderFactorySet Providers { get; }
    public ISystemMessengerSet Messages { get; }

    public Commander(ProviderFactorySet providers, ISystemMessengerSet messages,
                     CommandCollection commands, CommanderDomain domain) {
        Commands = commands ?? throw new ArgumentNullException(nameof(commands));
        Domain = domain;
        Messages = messages;
        Providers = providers;
    }

    public async Task<bool> AddPanel(string id, CancellationToken token) {
        if (Disposing) {
            return false;
        }
        if (Log.Info()) {
            Log.Info(Log.Join(nameof(AddPanel), id));
        }
        var panel = await PanelCollection.Add(id, token);
        if (panel == null) {
            return false;
        }
        return true;
    }

    public async Task<bool> RemovePanel(IPanel panel, CancellationToken token) {
        if (Log.Info()) {
            Log.Info(Log.Join(nameof(RemovePanel), panel));
        }
        return await PanelCollection.Remove(panel);
    }

    public async Task<bool> ShiftPanel(IPanel panel, int column, CancellationToken token) {
        if (Log.Info()) {
            Log.Info(Log.Join(nameof(ShiftPanel), panel));
        }
        return await PanelCollection.Shift(panel, column);
    }

    public async Task<bool> ClearPanels(CancellationToken token) {
        if (Log.Info()) {
            Log.Info(nameof(ClearPanels));
        }
        var panels = Enumerable.Range(0, PanelCollection.Count).Select(i => PanelCollection[i]).ToList();
        var removed = false;
        foreach (var panel in panels) {
            removed = await RemovePanel(panel, token) || removed;
        }
        return removed;
    }

    public async Task<bool> ShowPalette(CommandSource source, ICommandPaletteConfig config, CancellationToken token) {
        if (Palette != null) {
            await Task.CompletedTask;
            return false;
        }
        var palette = new CommandPalette(this, source);
        void palette_Escaping(object sender, EventArgs e) {
            palette.Escaping -= palette_Escaping;
            Palette = null;
            PanelCollection.Active?.Activate();
        }
        palette.Escaping += palette_Escaping;
        palette.Input.Text = config?.Input;
        palette.Input.SelectedStartOnLoad = config?.SelectedStart ?? 0;
        palette.Input.SelectedLengthOnLoad = config?.SelectedLength ?? 0;
        palette.Input.SuggestCommands = config?.SuggestCommands;
        Palette = palette;
        return true;
    }

    public void Close() {
        Controller?.CloseWindow();
    }

    public object NativeWindow() {
        return Controller?.NativeWindow();
    }

    bool ICommander.HasOperations(out IOperationCollection collection) {
        collection = Operator.Operations;
        return collection.Count > 0;
    }

    bool ICommander.HasWindow(out object native) {
        native = NativeWindow();
        return native != null;
    }

    ValueTask IAsyncDisposable.DisposeAsync() {
        lock (DisposalLocker) {
            var canRetry = DisposalTask is null || DisposalTask.IsFaulted || DisposalTask.IsCanceled;
            if (canRetry) {
                Disposing = true;
                DisposalTask = DisposeCoreAsync();
            }
            return new ValueTask(DisposalTask);
        }
    }
    ICommanderController IControlled<ICommanderController>.Controller {
        set {
            var oldValue = Controller;
            var newValue = value;
            if (Change(ref Controller, newValue, nameof(Controller))) {
                if (oldValue != null) {
                    oldValue.Loaded -= Controller_Loaded;
                    oldValue.WindowInput -= Controller_WindowInput;
                    oldValue.WindowClosed -= Controller_WindowClosed;
                    oldValue.WindowClosing -= Controller_WindowClosing;
                    oldValue.WindowGesture -= Controller_WindowGesture;
                }
                if (newValue != null) {
                    newValue.Loaded += Controller_Loaded;
                    newValue.WindowInput += Controller_WindowInput;
                    newValue.WindowClosed += Controller_WindowClosed;
                    newValue.WindowClosing += Controller_WindowClosing;
                    newValue.WindowGesture += Controller_WindowGesture;
                }
            }
        }
    }
    private ICommanderController Controller;
}
