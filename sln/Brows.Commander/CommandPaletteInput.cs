using Brows.Gui;
using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class CommandPaletteInput : Notifier, ICommandPalette, IControlled<ICommandPaletteInputController> {
        private int DelayState;
        private bool TextSuggestions = true;
        private CancellationTokenSource SuggestionTokenSource;
        private CommandSuggestionCollector SuggestionCollector;
        private readonly CommandSuggestionCollection SuggestionCollection;

        private Dictionary<IGesture, Action> GestureAction =>
            _GestureAction ?? (
            _GestureAction = new() {
                { new PressGesture(PressKey.Tab), Tab },
                { new PressGesture(PressKey.Enter), Enter },
                { new PressGesture(PressKey.Escape), Escape },
                { new PressGesture(PressKey.Down), Down },
                { new PressGesture(PressKey.Up), Up },
                { new PressGesture(PressKey.PageDown), PageDown },
                { new PressGesture(PressKey.PageUp), PageUp },
            });
        private Dictionary<IGesture, Action> _GestureAction;

        private void Conf_TextChanged(object sender, EventArgs e) {
            Context = null;
            ContextSuggestion = null;
        }

        private void Conf_Escaped(object sender, EventArgs e) {
            Controller?.Focus();
        }

        private void Conf_EscapedUp(object sender, EventArgs e) {
            Controller?.Focus();
        }

        private void Controller_Loaded(object sender, EventArgs e) {
            var selectedLength = SelectedLengthOnLoad;
            if (selectedLength > 0) {
                var selectedStart = SelectedStartOnLoad;
                if (selectedStart >= 0) {
                    Controller?.SelectText(selectedStart, selectedLength);
                }
            }
        }

        private void Controller_Unloaded(object sender, EventArgs e) {
        }

        private void Controller_Gesture(object sender, GestureEventArgs e) {
            if (e != null) {
                var act = e.Triggered = GestureAction.TryGetValue(e.Gesture, out var action);
                if (act) {
                    action();
                }
            }
        }

        private void SuggestionCollection_CurrentSuggestionChanged(object sender, EventArgs e) {
            var suggestion = SuggestionCollection.CurrentSuggestion;
            if (suggestion != null) {
                var input = (suggestion.Input ?? "").Trim();
                if (input != "") {
                    TakeSuggestion(input);
                }
            }
        }

        private void Down() {
            if (Conf.Text == null) {
                SuggestionCollection.MoveCurrentSuggestion(PressKey.Down);
            }
            else {
                Conf.Focus();
            }
        }

        private void Up() {
            if (Conf.Text == null) {
                SuggestionCollection.MoveCurrentSuggestion(PressKey.Up);
            }
        }

        private void PageDown() {
            if (Conf.Text == null) {
                SuggestionCollection.MoveCurrentSuggestion(PressKey.PageDown);
            }
        }

        private void PageUp() {
            if (Conf.Text == null) {
                SuggestionCollection.MoveCurrentSuggestion(PressKey.PageUp);
            }
        }

        private void Tab() {
            if (TextSuggestion != null) {
                Text = TextSuggestion;
            }
            Controller?.MoveCaret(Text.Length);
        }

        private bool Enter(CommandContext context) {
            if (context == null) {
                return false;
            }
            var triggered = context.TriggeringCommands;
            var triggeredCommand = triggered.FirstOrDefault(command => command.TriggeredWork(context));
            if (triggeredCommand == null) {
                var arbitraryCommand = Commander.Commands.AsEnumerable().FirstOrDefault(command => command.ArbitraryWork(context));
                if (arbitraryCommand == null) {
                    return false;
                }
            }
            return true;
        }

        private void Enter() {
            var context = default(CommandContext);
            var entered = Enter(context = Context);
            if (entered == false) {
                var enteredSuggestion = Enter(context = ContextSuggestion);
                if (enteredSuggestion == false) {
                    return;
                }
            }
            if (context.HasFlag(out var flag) && flag.PersistInput) {
                if (flag.RefreshInput) {
                    var
                    text = Text;
                    Text = "";
                    Text = flag.SetInput ?? text;
                    Controller?.MoveCaret(Text.Length);
                    if (flag.SelectInputLength > 0) {
                        Controller?.SelectText(flag.SelectInputStart, flag.SelectInputLength);
                    }
                }
            }
            else {
                Escape();
            }
        }

        private void SuggestionCollector_InputSuggestionChanged(object sender, EventArgs e) {
            var inputSuggestion = SuggestionCollector?.InputSuggestion;
            if (inputSuggestion != null) {
                TextSuggestion = inputSuggestion;
                MatchSuggestion();
            }
        }

        private void MatchSuggestion() {
            var textSuggestion = TextSuggestion;
            if (textSuggestion != null && textSuggestion.Length > 0) {
                var text = Text;
                if (text != null && text.Length > 0) {
                    if (textSuggestion.StartsWith(text, StringComparison.CurrentCultureIgnoreCase)) {
                        var length = textSuggestion.Length;
                        var exactMatch = new char[length];
                        for (var i = 0; i < length; i++) {
                            exactMatch[i] = text.Length > i
                                ? text[i]
                                : textSuggestion[i];
                        }
                        TextSuggestion = new string(exactMatch);
                    }
                    else {
                        TextSuggestion = null;
                    }
                }
            }
        }

        private async void Set(string text) {
            if (TextSuggestions) {
                MatchSuggestion();
                var delayState = ++DelayState;
                var delay = Delay;
                if (delay > 0) {
                    var delayToken = SuggestionTokenSource?.Token ?? default;
                    try {
                        await Task.Delay(delay, delayToken);
                    }
                    catch (OperationCanceledException canceled) when (canceled?.CancellationToken == delayToken) {
                        return;
                    }
                }
                if (delayState != DelayState) {
                    return;
                }
                DelayState = 0;
                TextSuggestion = null;
                if (SuggestionTokenSource != null) {
                    SuggestionTokenSource.Cancel();
                }
                if (SuggestionCollector != null) {
                    SuggestionCollector.InputSuggestionChanged -= SuggestionCollector_InputSuggestionChanged;
                    SuggestionCollector = null;
                }
                using (var tokenSource = SuggestionTokenSource = new()) {
                    var token = tokenSource.Token;
                    try {
                        SuggestionCollection.Clear();
                        Suggesting?.Invoke(this, EventArgs.Empty);
                        SuggestionCollector = new CommandSuggestionCollector(Context, Commander.Commands.AsEnumerable());
                        SuggestionCollector.Collection = SuggestionCollection;
                        SuggestionCollector.InputSuggestionChanged += SuggestionCollector_InputSuggestionChanged;
                        await SuggestionCollector.Collect(token);
                    }
                    catch (OperationCanceledException canceled) when (canceled.CancellationToken == token) {
                    }
                    finally {
                        SuggestionTokenSource = null;
                    }
                }
            }
        }

        public event EventHandler Escaping;
        public event EventHandler Suggesting;

        public object Suggestions =>
            SuggestionCollection;

        public int Delay {
            get => _Delay;
            set => Change(ref _Delay, value, nameof(Delay));
        }
        private int _Delay = 100;

        public string Text {
            get => _Text ?? (_Text = "");
            set {
                if (Change(ref _Text, value, nameof(Text))) {
                    Context = null;
                    Set(text: _Text);
                }
            }
        }
        private string _Text;

        public string TextSuggestion {
            get => _TextSuggestion;
            private set {
                if (Change(ref _TextSuggestion, value, nameof(TextSuggestion))) {
                    ContextSuggestion = null;
                }
            }
        }
        private string _TextSuggestion;

        public int SelectedStartOnLoad {
            get => _SelectedStartOnLoad;
            set => Change(ref _SelectedStartOnLoad, value, nameof(SelectedStartOnLoad));
        }
        private int _SelectedStartOnLoad;

        public int SelectedLengthOnLoad {
            get => _SelectedLengthOnLoad;
            set => Change(ref _SelectedLengthOnLoad, value, nameof(SelectedLengthOnLoad));
        }
        private int _SelectedLengthOnLoad;

        public Commander Commander { get; }
        public CommandSource Source { get; }
        public CommandPaletteConf Conf { get; }

        public CommandPaletteInput(Commander commander, CommandSource source) {
            Commander = commander;
            Source = source;
            SuggestionCollection = new();
            SuggestionCollection.CurrentSuggestionChanged += SuggestionCollection_CurrentSuggestionChanged;
            Conf = new();
            Conf.Escaped += Conf_Escaped;
            Conf.EscapedUp += Conf_EscapedUp;
            Conf.TextChanged += Conf_TextChanged;
        }

        public CommandContext Context {
            get =>
                _Context ?? (
                _Context = new CommandContext(Commander, Source, new CommandLine(Text, Conf.Text), this));
            private set =>
                Change(ref _Context, value, nameof(Context));
        }
        private CommandContext _Context;

        public CommandContext ContextSuggestion {
            get =>
                _ContextSuggestion ?? (
                _ContextSuggestion = TextSuggestion == null
                    ? null
                    : new CommandContext(Commander, Source, new CommandLine(TextSuggestion, Conf.Text), this));
            private set =>
                Change(ref _ContextSuggestion, value, nameof(ContextSuggestion));
        }
        private CommandContext _ContextSuggestion;

        public void TakeSuggestion(string text) {
            TextSuggestions = false;
            Text = text;
            TextSuggestion = Text;
            TextSuggestions = true;
            Controller?.MoveCaret(Text.Length);
        }

        public void Escape() {
            Escaping?.Invoke(this, EventArgs.Empty);
            if (SuggestionTokenSource != null) {
                SuggestionTokenSource.Cancel();
            }
        }

        public void Select(int start, int length) {
            Controller?.SelectText(start, length);
        }

        ICommandPaletteInputController IControlled<ICommandPaletteInputController>.Controller {
            set {
                var newValue = value;
                var oldValue = Controller;
                if (oldValue != newValue) {
                    if (oldValue != null) {
                        oldValue.Loaded -= Controller_Loaded;
                        oldValue.Unloaded -= Controller_Unloaded;
                        oldValue.Gesture -= Controller_Gesture;
                    }
                    if (newValue != null) {
                        newValue.Loaded += Controller_Loaded;
                        newValue.Unloaded += Controller_Unloaded;
                        newValue.Gesture += Controller_Gesture;
                    }
                    Controller = newValue;
                }
            }
        }
        private ICommandPaletteInputController Controller;
    }
}
