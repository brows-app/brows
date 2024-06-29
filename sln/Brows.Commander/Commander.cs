using Brows.Gui;
using Domore.Logs;
using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class Commander : Notifier, ICommander, IControlled<ICommanderController> {
        private static readonly ILog Log = Logging.For(typeof(Commander));

        private MessageSet MessageSet;

        private PanelCollection PanelCollection =>
            _PanelCollection ?? (
            _PanelCollection = new PanelCollection(Providers, this));
        private PanelCollection _PanelCollection;

        private async void Controller_Loaded(object sender, EventArgs e) {
            var token = CancellationToken.None;
            Loaded?.Invoke(this, e);

            var messageSet = MessageSet;
            if (messageSet != null) {
                messageSet.Message -= MessageSet_Message;
                messageSet.Dispose();
            }
            MessageSet = await Messages.Create(Controller?.NativeWindow(), token);
            MessageSet.Message += MessageSet_Message;

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

        private void Controller_WindowClosed(object sender, EventArgs e) {
            PanelCollection.Clear();
            Closed?.Invoke(this, e);
        }

        private void Controller_WindowClosing(object sender, EventArgs e) {
            MessageSet?.Dispose();
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

        private async void MessageSet_Message(object source, MessageEventArgs e) {
            var token = CancellationToken.None;
            var message = e?.Message;
            if (message != null) {
                IEnumerable<Task> tasks() {
                    foreach (var panel in PanelCollection) {
                        async Task task() {
                            try {
                                await panel.Post(message, token);
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

        private bool Input(IGesture gesture, object source) {
            if (Palette != null) {
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
            get => _First;
            set => Change(ref _First, value, nameof(First));
        }
        private bool _First;

        public IReadOnlyList<string> Load {
            get => _Load ?? (_Load = Array.Empty<string>());
            set => _Load = value;
        }
        private IReadOnlyList<string> _Load;

        public CommandPalette Palette {
            get => _Palette;
            private set => Change(ref _Palette, value, nameof(Palette));
        }
        private CommandPalette _Palette;

        public bool DialogFocused {
            get => _DialogFocused;
            private set => Change(ref _DialogFocused, value, nameof(DialogFocused));
        }
        private bool _DialogFocused;

        public Operator Operator =>
            _Operator ?? (
            _Operator = new Operator());
        private Operator _Operator;

        public IPanelCollection Panels =>
            PanelCollection;

        public CommanderDomain Domain { get; }
        public CommandCollection Commands { get; }
        public MessageSetFactory Messages { get; }
        public ProviderFactorySet Providers { get; }

        public Commander(ProviderFactorySet providers, MessageSetFactory messages, CommandCollection commands, CommanderDomain domain) {
            Commands = commands ?? throw new ArgumentNullException(nameof(commands));
            Domain = domain;
            Messages = messages;
            Providers = providers;
        }

        public async Task<bool> AddPanel(string id, CancellationToken token) {
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
}
