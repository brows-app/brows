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

        private PanelCollection PanelCollection =>
            _PanelCollection ?? (
            _PanelCollection = new PanelCollection(Providers, this));
        private PanelCollection _PanelCollection;

        private async void Controller_Loaded(object sender, EventArgs e) {
            Loaded?.Invoke(this, e);
            var load = Load.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
            foreach (var item in load) {
                await AddPanel(item, CancellationToken.None);
            }
            if (First) {
                await ShowPalette(null, "? ", 0, 2, CancellationToken.None);
            }
        }

        private void Controller_WindowClosed(object sender, EventArgs e) {
            Closed?.Invoke(this, e);
        }

        private void Controller_WindowInput(object sender, InputEventArgs e) {
            if (e != null) {
                e.Triggered = Input(e.Text);
            }
        }

        private void Controller_WindowGesture(object sender, GestureEventArgs e) {
            if (e != null) {
                e.Triggered = Input(e.Gesture, e.Source);
            }
        }

        private bool Input(IGesture gesture, object source) {
            if (Palette != null) {
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
            var triggered = Commands.Triggered(gesture, out var commands);
            if (triggered) {
                foreach (var command in commands) {
                    var shortcut = command.Trigger.Gesture[gesture].Shortcut;
                    var cmdLine = new CommandLine(shortcut, null);
                    var context = new CommandContext(this, new CommandSource(source), cmdLine, gesture);
                    var work = command.TriggeredWork(context);
                    if (work) {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool Input(string text) {
            if (Palette != null) {
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

        public Operator Operator =>
            _Operator ?? (
            _Operator = new Operator());
        private Operator _Operator;

        public IPanelCollection Panels =>
            PanelCollection;

        public CommanderDomain Domain { get; }
        public CommandCollection Commands { get; }
        public ProviderFactorySet Providers { get; }

        public Commander(ProviderFactorySet providers, CommandCollection commands, CommanderDomain domain) {
            Commands = commands ?? throw new ArgumentNullException(nameof(commands));
            Domain = domain;
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

        public async Task<bool> ShowPalette(CommandSource source, string input, int selectedStart, int selectedLength, CancellationToken token) {
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
            palette.Input.Text = input;
            palette.Input.SelectedStartOnLoad = selectedStart;
            palette.Input.SelectedLengthOnLoad = selectedLength;
            Palette = palette;
            return true;
        }

        public void Close() {
            Controller?.CloseWindow();
        }

        bool ICommander.HasOperations(out IOperationCollection collection) {
            collection = Operator.Operations;
            return collection.Count > 0;
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
                        oldValue.WindowGesture -= Controller_WindowGesture;
                    }
                    if (newValue != null) {
                        newValue.Loaded += Controller_Loaded;
                        newValue.WindowInput += Controller_WindowInput;
                        newValue.WindowClosed += Controller_WindowClosed;
                        newValue.WindowGesture += Controller_WindowGesture;
                    }
                }
            }
        }
        private ICommanderController Controller;
    }
}
