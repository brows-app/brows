using Brows.Gui;
using Domore.Logs;
using Domore.Notification;
using System;
using System.Collections.Generic;

namespace Brows {
    internal sealed class CommandPalette : Notifier, IControlled<ICommandPaletteController> {
        private static readonly ILog Log = Logging.For(typeof(CommandPalette));

        private void Input_Escaping(object sender, EventArgs e) {
            Escaping?.Invoke(this, e);
        }

        private void Input_Suggesting(object sender, EventArgs e) {
            SuggestionConf = Input.Context.DidTrigger(SuggestionConf?.Command) ? SuggestionConf : null;
            SuggestionData = Input.Context.DidTrigger(SuggestionData?.Command) ? SuggestionData : null;
            SuggestionHint = Input.Context.DidTrigger(SuggestionHint?.Command) ? SuggestionHint : null;
        }

        private Dictionary<IGesture, Action> GestureAction =>
            _GestureAction ?? (
            _GestureAction = new Dictionary<IGesture, Action> {
                { CommandContextDataGesture.Next,
                    () => Change(SuggestionData?.Next()) },
                { CommandContextDataGesture.Previous,
                    () => Change(SuggestionData?.Previous()) },
                { CommandContextDataGesture.Enter,
                    () => Change(SuggestionData?.Enter()) },
                { CommandContextDataGesture.Up,
                    () => Controller?.ScrollSuggestionData(PressKey.Up) },
                { CommandContextDataGesture.Down,
                    () => Controller?.ScrollSuggestionData(PressKey.Down) },
                { CommandContextDataGesture.PageUp,
                    () => Controller?.ScrollSuggestionData(PressKey.PageUp) },
                { CommandContextDataGesture.PageDown,
                    () => Controller?.ScrollSuggestionData(PressKey.PageDown) },
                { CommandContextDataGesture.Remove,
                    () => Change(SuggestionData?.Remove()) },
                { CommandContextDataGesture.Clear,
                    () => Change(SuggestionData?.Clear()) }
            });
        private Dictionary<IGesture, Action> _GestureAction;

        private void Change(ICommandContextData data) {
            var escape = false;
            if (data != null) {
                var flag = data.Flag;
                if (flag != null) {
                    if (flag.PersistInput == false) {
                        Input.Escape();
                        escape = true;
                    }
                }
            }
            SuggestionData = escape
                ? null
                : data;
        }

        private void Controller_Gesture(object sender, GestureEventArgs e) {
            if (e != null) {
                if (GestureAction.TryGetValue(e.Gesture, out var action)) {
                    action();
                    e.Triggered = true;
                }
            }
        }

        public event EventHandler Escaping;

        public ICommandContextData SuggestionData {
            get => _SuggestionData;
            set {
                if (Change(ref _SuggestionData, value, nameof(SuggestionData))) {
                    var input = _SuggestionData?.Input;
                    if (input != null) {
                        Input.TakeSuggestion(input);
                    }
                    var conf = _SuggestionData?.Conf;
                    if (conf != null) {
                        Input.Conf.Suggestion = new CommandContextConf(_SuggestionData.Command, conf);
                    }
                }
            }
        }
        private ICommandContextData _SuggestionData;

        public ICommandContextConf SuggestionConf {
            get => _SuggestionConf;
            set {
                if (Change(ref _SuggestionConf, value, nameof(SuggestionConf))) {
                    Input.Conf.Suggestion = _SuggestionConf;
                }
            }
        }
        private ICommandContextConf _SuggestionConf;

        public ICommandContextHint SuggestionHint {
            get => _SuggestionHint;
            set => Change(ref _SuggestionHint, value, nameof(SuggestionHint));
        }
        private ICommandContextHint _SuggestionHint;

        public Commander Commander { get; }
        public CommandPaletteInput Input { get; }

        public CommandPalette(Commander commander) {
            Commander = commander;
            Input = new(Commander);
            Input.Escaping += Input_Escaping;
            Input.Suggesting += Input_Suggesting;
        }

        ICommandPaletteController IControlled<ICommandPaletteController>.Controller {
            set {
                var newValue = value;
                var oldValue = Controller;
                if (oldValue != newValue) {
                    if (oldValue != null) {
                        oldValue.Gesture -= Controller_Gesture;
                    }
                    if (newValue != null) {
                        newValue.Gesture += Controller_Gesture;
                    }
                    Controller = newValue;
                }
            }
        }
        private ICommandPaletteController Controller;
    }
}
