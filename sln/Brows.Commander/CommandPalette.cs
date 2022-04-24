using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using ComponentModel;
    using Gui;
    using Logger;
    using Threading.Tasks;
    using Triggers;

    internal class CommandPalette : NotifyPropertyChanged, ICommandPalette, IControlled<ICommandPaletteController> {
        private int InputDelay = 100;
        private bool InputSuggesting = true;
        private object InputState = new object();
        private CommandSuggestionCollector SuggestionCollector;

        private ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(CommandPalette)));
        private ILog _Log;

        private TaskHandler TaskHandler =>
            _TaskHandler ?? (
            _TaskHandler = new TaskHandler<CommandPalette>());
        private TaskHandler _TaskHandler;

        private CommandSuggestionCollection SuggestionCollection =>
            _SuggestionCollection ?? (
            _SuggestionCollection = new CommandSuggestionCollection());
        private CommandSuggestionCollection _SuggestionCollection;

        private Dictionary<KeyboardGesture, Action> KeyMap =>
            _KeyMap ?? (
            _KeyMap = new Dictionary<KeyboardGesture, Action> {
                { new KeyboardGesture(KeyboardKey.Enter, KeyboardModifiers.None),
                    () => TaskHandler.Begin(Enter) },
                { new KeyboardGesture(KeyboardKey.Escape, KeyboardModifiers.None),
                    Escape },
                { new KeyboardGesture(KeyboardKey.Tab, KeyboardModifiers.None),
                    Tab },
                { new KeyboardGesture(KeyboardKey.Right, KeyboardModifiers.Alt),
                    () => SuggestionData = SuggestionData?.Next() },
                { new KeyboardGesture(KeyboardKey.Left, KeyboardModifiers.Alt),
                    () => SuggestionData = SuggestionData?.Previous() },
                { new KeyboardGesture(KeyboardKey.Enter, KeyboardModifiers.Alt),
                    () => SuggestionData?.Enter() },
                { new KeyboardGesture(KeyboardKey.Up, KeyboardModifiers.Alt),
                    () => SuggestionData?.Up() },
                { new KeyboardGesture(KeyboardKey.Down, KeyboardModifiers.Alt),
                    () => SuggestionData?.Down() },
                { new KeyboardGesture(KeyboardKey.PageUp, KeyboardModifiers.Alt),
                    () => SuggestionData?.PageUp() },
                { new KeyboardGesture(KeyboardKey.PageDown, KeyboardModifiers.Alt),
                    () => SuggestionData?.PageDown() },
                { new KeyboardGesture(KeyboardKey.D3, KeyboardModifiers.Shift | KeyboardModifiers.Alt),
                    () => SuggestionData = SuggestionData?.Remove() },
                { new KeyboardGesture(KeyboardKey.D3, KeyboardModifiers.Shift | KeyboardModifiers.Alt | KeyboardModifiers.Control),
                    () => SuggestionData = SuggestionData?.RemoveAll() }
            });
        private Dictionary<KeyboardGesture, Action> _KeyMap;

        private CommandContext CreateContext(string input) {
            var inp = input?.Trim() ?? "";
            var info = new CommandInfo(inp);
            var trigger = new InputTrigger(info.Command);
            return new CommandContext(Commander, trigger, info);
        }

        private void Control_Loaded(object sender, EventArgs e) {
            OnLoaded(e);
        }

        private void Control_CurrentSuggestionChanged(object sender, EventArgs e) {
            var suggestion = Controller.CurrentSuggestion;
            if (suggestion != null) {
                var input = (suggestion.Input ?? "").Trim();
                if (input != "") {
                    InputSuggesting = false;
                    Input = input;
                    InputSuggestion = Input;
                    InputSuggesting = true;
                }
            }
        }

        private void Control_LostFocus(object sender, EventArgs e) {
            Escape();
        }

        private void Window_KeyboardKeyDown(object sender, KeyboardKeyEventArgs e) {
            if (e != null) {
                if (KeyMap.TryGetValue(e.Gesture, out var action)) {
                    action();
                    e.Triggered = true;
                }
            }
        }

        private void SuggestionCollector_InputSuggestionChanged(object sender, EventArgs e) {
            var inputSuggestion = SuggestionCollector?.InputSuggestion;
            if (inputSuggestion != null) {
                InputSuggestion = inputSuggestion;
                MatchSuggestion();
            }
        }

        private void MatchSuggestion() {
            var inputSuggestion = InputSuggestion;
            if (inputSuggestion != null && inputSuggestion.Length > 0) {
                var input = Input;
                if (input != null && input.Length > 0) {
                    if (inputSuggestion.StartsWith(input, StringComparison.CurrentCultureIgnoreCase)) {
                        var length = inputSuggestion.Length;
                        var exactMatch = new char[length];
                        for (var i = 0; i < length; i++) {
                            exactMatch[i] = input.Length > i
                                ? input[i]
                                : inputSuggestion[i];
                        }
                        InputSuggestion = new string(exactMatch);
                    }
                    else {
                        InputSuggestion = null;
                    }
                }
            }
        }

        private void StopSuggesting() {
            var collector = SuggestionCollector;
            if (collector != null) {
                collector.InputSuggestionChanged -= SuggestionCollector_InputSuggestionChanged;
                collector.End();
                collector.Dispose();
            }
            SuggestionCollector = null;
        }

        private void StartSuggesting(string input) {
            if (Log.Info()) {
                Log.Info(
                    nameof(StartSuggesting),
                    $"{nameof(input)} > {input}");
            }
            SuggestionContext = CreateContext(input);
            SuggestionData = SuggestionContext.DidTrigger(SuggestionData?.Command) ? SuggestionData : null;
            SuggestionHint = SuggestionContext.DidTrigger(SuggestionHint?.Command) ? SuggestionHint : null;
            SuggestionCollection.Clear();
            SuggestionCollector = new CommandSuggestionCollector(SuggestionContext, Commander.Commands);
            SuggestionCollector.Collection = SuggestionCollection;
            SuggestionCollector.InputSuggestionChanged += SuggestionCollector_InputSuggestionChanged;
            SuggestionCollector.Begin();
        }

        private void Tab() {
            Input = InputSuggestion;
            Controller?.MoveCaret(Input.Length);
        }

        private void Escape() {
            StopSuggesting();
            OnEscaping(EventArgs.Empty);
            SuggestionHint = null;
            SuggestionData = null;
            SuggestionContext = null;
            SuggestionCollector = null;
        }

        private async Task<bool> Execute(IEnumerable<ICommand> options, ICommandContext context) {
            foreach (var command in options) {
                var worked = await command.WorkAsync(context, CancellationToken.None);
                if (worked) {
                    if (context.HasFlag(out var flag)) {
                        if (flag.PersistInput) {
                            if (flag.RefreshInput) {
                                var
                                input = Input;
                                Input = "";
                                Input = flag.SetInput ?? input;
                                Controller?.MoveCaret(Input.Length);
                                if (flag.SelectInputLength > 0) {
                                    Controller?.SelectText(flag.SelectInputStart, flag.SelectInputLength);
                                }
                            }
                        }
                    }
                    else {
                        Escape();
                    }
                    return true;
                }
            }
            return false;
        }

        private async Task<bool> Enter(string input) {
            if (string.IsNullOrWhiteSpace(input)) {
                return false;
            }
            var context = CreateContext(input);
            var commands = Commander.Commands;
            if (commands.Triggered(context.Trigger, out var triggered)) {
                var triggeredCommands = triggered.Where(c => c.Workable(context));
                var triggeredCommandExecuted = await Execute(triggeredCommands, context);
                if (triggeredCommandExecuted) {
                    return true;
                }
            }
            if (commands.Arbitrary(out var arbitrary)) {
                var arbitraryCommands = arbitrary.Where(c => c.Workable(context));
                var arbitraryCommandExecuted = await Execute(arbitraryCommands, context);
                if (arbitraryCommandExecuted) {
                    return true;
                }
            }
            return false;
        }

        private async Task<bool> Enter() {
            if (Log.Info()) {
                Log.Info(nameof(Enter));
            }
            var input = Input;
            var entered = await Enter(input);
            if (entered) {
                if (Log.Info()) {
                    Log.Info($"{nameof(entered)} > {input}");
                }
                return true;
            }
            var inputSuggestion = InputSuggestion;
            var enteredSuggestion = await Enter(inputSuggestion);
            if (enteredSuggestion) {
                if (Log.Info()) {
                    Log.Info($"{nameof(enteredSuggestion)} > {inputSuggestion}");
                }
                return true;
            }
            return false;
        }

        protected virtual void OnLoaded(EventArgs e) {
            Loaded?.Invoke(this, e);
        }

        protected virtual void OnEscaping(EventArgs e) {
            Escaping?.Invoke(this, e);
        }

        public event EventHandler Loaded;
        public event EventHandler Escaping;

        public object SuggestionSource =>
            SuggestionCollection.Source;

        public ICommandContext SuggestionContext {
            get => _SuggestionContext;
            private set => Change(ref _SuggestionContext, value, nameof(SuggestionContext));
        }
        private ICommandContext _SuggestionContext;

        public ICommandContextData SuggestionData {
            get => _SuggestionData;
            set {
                if (Change(ref _SuggestionData, value, nameof(SuggestionData))) {
                    var input = _SuggestionData?.Input;
                    if (input != null) {
                        InputSuggesting = false;
                        Input = input;
                        InputSuggestion = Input;
                        InputSuggesting = true;
                    }
                }
            }
        }
        private ICommandContextData _SuggestionData;

        public ICommandContextHint SuggestionHint {
            get => _SuggestionHint;
            set => Change(ref _SuggestionHint, value, nameof(SuggestionHint));
        }
        private ICommandContextHint _SuggestionHint;

        public ICommandPaletteController Controller {
            get => _Controller;
            set {
                var oldValue = _Controller;
                var newValue = value;
                if (Change(ref _Controller, newValue, nameof(Controller))) {
                    if (oldValue != null) {
                        oldValue.Loaded -= Control_Loaded;
                        oldValue.CurrentSuggestionChanged -= Control_CurrentSuggestionChanged;
                        oldValue.LostFocus -= Control_LostFocus;
                        oldValue.WindowKeyboardKeyDown -= Window_KeyboardKeyDown;
                    }
                    if (newValue != null) {
                        newValue.Loaded += Control_Loaded;
                        newValue.CurrentSuggestionChanged += Control_CurrentSuggestionChanged;
                        newValue.LostFocus += Control_LostFocus;
                        newValue.WindowKeyboardKeyDown += Window_KeyboardKeyDown;
                    }
                }
            }
        }
        private ICommandPaletteController _Controller;

        public string InputSuggestion {
            get => _InputSuggestion;
            private set => Change(ref _InputSuggestion, value, nameof(InputSuggestion));
        }
        private string _InputSuggestion;

        public string Input {
            get => _Input ?? (_Input = "");
            set {
                if (Change(ref _Input, value, nameof(Input))) {
                    if (InputSuggesting) {
                        MatchSuggestion();
                        TaskHandler.Begin(async () => {
                            var state = InputState = new object();
                            await Task.Delay(InputDelay);
                            if (InputState == state) {
                                InputSuggestion = null;
                                if (InputSuggesting) {
                                    StopSuggesting();
                                    StartSuggesting(_Input);
                                }
                            }
                        });
                    }
                }
            }
        }
        private string _Input;

        public Commander Commander { get; }

        public CommandPalette(Commander commander) {
            Commander = commander;
        }

        public void SelectInput(int start, int length) {
            Controller?.SelectText(start, length);
        }
    }
}
