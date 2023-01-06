using Domore.Logs;
using Domore.Notification;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Config;
    using Gui;
    using IO;
    using Threading.Tasks;
    using Triggers;

    public class Commander : Notifier, ICommander, IControlled<ICommanderController> {
        private static readonly ILog Log = Logging.For(typeof(Commander));

        private CommanderThemeConfig ThemeConfig =>
            _ThemeConfig ?? (
            _ThemeConfig = new CommanderThemeConfig());
        private CommanderThemeConfig _ThemeConfig;

        private TaskHandler TaskHandler =>
            _TaskHandler ?? (
            _TaskHandler = new TaskHandler<Commander>());
        private TaskHandler _TaskHandler;

        private PanelCollection PanelCollection =>
            _PanelCollection ?? (
            _PanelCollection = new PanelCollection());
        private PanelCollection _PanelCollection;

        private void Controller_Loaded(object sender, EventArgs e) {
            TaskHandler.Begin(async cancellationToken => {
                await AddPanel("", cancellationToken);
                if (First) {
                    await ShowPalette("?", 0, 1, cancellationToken);
                    Theme = await ThemeConfig.Load(cancellationToken);
                    Themed?.Invoke(this, EventArgs.Empty);
                }
            });
            OnLoaded(e);
        }

        private void Controller_WindowClosed(object sender, EventArgs e) {
            OnClosed(e);
        }

        private void Controller_WindowInput(object sender, InputEventArgs e) {
            if (e != null) {
                e.Triggered = Input(e.Text);
            }
        }

        private void Controller_WindowKeyboardKeyDown(object sender, KeyboardKeyEventArgs e) {
            if (e != null) {
                e.Triggered = Input(e.Key, e.Modifiers);
            }
        }

        private bool Input(KeyboardKey key, KeyboardModifiers modifiers) {
            if (Palette != null) return false;
            if (Dialog?.Current != null) return false;

            var panel = Panels.Active;
            if (panel == null) {
                panel = Panels.Count > 0 ? Panels[0] : null;
            }
            if (panel != null) {
                if (panel.Activated == false) {
                    panel.Activate();
                }
            }
            var trigger = new KeyboardTrigger(key, modifiers);
            if (Commands.Triggered(trigger, out var commands)) {
                var context = new CommandContext(this, trigger);
                foreach (var command in commands) {
                    if (command.Workable(context)) {
                        command.Work(context, default).ContinueWith(task => {
                            var exception = task.Exception;
                            if (exception != null) {
                                if (Log.Warn()) {
                                    Log.Warn(exception);
                                }
                            }
                        });
                        return true;
                    }
                }
            }
            return false;
        }

        private bool Input(string text) {
            if (Palette != null) return false;
            if (Dialog?.Current != null) return false;
            if (Log.Info()) {
                Log.Info(
                    nameof(Input),
                    $"{nameof(text)} > {text}");
            }
            var panel = Panels.Active;
            if (panel == null) return false;

            return panel.Input(text);
        }

        protected virtual void OnLoaded(EventArgs e) {
            Loaded?.Invoke(this, e);
        }

        protected virtual void OnLogger(EventArgs e) {
            Logger?.Invoke(this, e);
        }

        protected virtual void OnClosed(EventArgs e) {
            Closed?.Invoke(this, e);
        }

        protected virtual void OnExited(EventArgs e) {
            Exited?.Invoke(this, e);
        }

        public event EventHandler Loaded;
        public event EventHandler Logger;
        public event EventHandler Closed;
        public event EventHandler Exited;
        public event EventHandler Themed;

        public ICommanderController Controller {
            get => _Controller;
            set {
                var oldValue = _Controller;
                var newValue = value;
                if (Change(ref _Controller, newValue, nameof(Controller))) {
                    if (oldValue != null) {
                        oldValue.Loaded -= Controller_Loaded;
                        oldValue.WindowClosed -= Controller_WindowClosed;
                        oldValue.WindowInput -= Controller_WindowInput;
                        oldValue.WindowKeyboardKeyDown -= Controller_WindowKeyboardKeyDown;
                    }
                    if (newValue != null) {
                        newValue.Loaded += Controller_Loaded;
                        newValue.WindowClosed += Controller_WindowClosed;
                        newValue.WindowInput += Controller_WindowInput;
                        newValue.WindowKeyboardKeyDown += Controller_WindowKeyboardKeyDown;
                    }
                    var dialog = Dialog;
                    if (dialog != null) {
                        dialog.Window = newValue?.NativeWindow();
                    }
                }
            }
        }
        private ICommanderController _Controller;

        public CommanderTheme Theme {
            get => _Theme;
            private set => Change(ref _Theme, value, nameof(Theme));
        }
        private CommanderTheme _Theme;

        public bool First {
            get => _First;
            set => Change(ref _First, value, nameof(First));
        }
        private bool _First;

        public ICommandPalette Palette {
            get => _Palette;
            private set => Change(ref _Palette, value, nameof(Palette));
        }
        private ICommandPalette _Palette;

        public IDialogState Dialog {
            get => _Dialog;
            set => Change(ref _Dialog, value, nameof(Dialog));
        }
        private IDialogState _Dialog;

        public IClipboard Clipboard {
            get => _Clipboard;
            set => Change(ref _Clipboard, value, nameof(Clipboard));
        }
        private IClipboard _Clipboard;

        public EntryProviderFactoryCollection EntryProviderFactory {
            get => _EntryProviderFactory ?? (_EntryProviderFactory = new EntryProviderFactoryCollection(new IEntryProviderFactory[] { }));
            set => Change(ref _EntryProviderFactory, value, nameof(EntryProviderFactory));
        }
        private EntryProviderFactoryCollection _EntryProviderFactory;

        public ICommandCollection Commands {
            get => _Commands ?? (_Commands = new CommandCollection(new ICommand[] { }));
            set => Change(ref _Commands, value, nameof(Commands));
        }
        private ICommandCollection _Commands;

        public IOperationCollection Operations =>
            _Operations ?? (
            _Operations = new OperationCollection());
        private IOperationCollection _Operations;

        public IPanelCollection Panels =>
            PanelCollection;

        public async Task AddPanel(string id, CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(
                    nameof(AddPanel),
                    $"{nameof(id)} > {id}");
            }
            var panel = await PanelCollection.Add(id, Dialog, Operations, EntryProviderFactory, cancellationToken);
            Controller?.AddPanel(panel);
        }

        public async Task RemovePanel(IPanel panel, CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(
                    nameof(RemovePanel),
                    $"{nameof(panel)} > {panel}");
            }
            PanelCollection.Remove(panel);
            Controller?.RemovePanel(panel);
            await Task.CompletedTask;
        }

        public Task ShowPalette(string input, CancellationToken cancellationToken) {
            return ShowPalette(input, 0, 0, cancellationToken);
        }

        public Task ShowPalette(string input, int selectedStart, int selectedLength, CancellationToken cancellationToken) {
            if (Palette == null) {
                var palette = new CommandPalette(this);
                void palette_Loaded(object sender, EventArgs e) {
                    if (selectedLength > 0) {
                        palette.SelectInput(selectedStart, selectedLength);
                    }
                }
                void palette_Escaping(object sender, EventArgs e) {
                    palette.Loaded -= palette_Loaded;
                    palette.Escaping -= palette_Escaping;
                    Palette = null;
                    Panels.Active?.Activate();
                }
                palette.Loaded += palette_Loaded;
                palette.Escaping += palette_Escaping;
                palette.Input = input;
                Palette = palette;
            }
            return Task.CompletedTask;
        }

        public void Close() {
            Controller?.CloseWindow();
        }

        public void Exit() {
            OnExited(EventArgs.Empty);
        }

        public async Task SetTheme(string @base, string background, string foreground, CancellationToken cancellationToken) {
            Theme = new CommanderTheme(@base, background, foreground);
            Themed?.Invoke(this, EventArgs.Empty);
            await ThemeConfig.Save(Theme, cancellationToken);
        }

        public async Task ShowLog(CancellationToken cancellationToken) {
            OnLogger(EventArgs.Empty);
            await Task.CompletedTask;
        }
    }
}
