using Brows.Gui;
using Domore.Notification;
using System;
using System.Collections.Generic;

namespace Brows {
    internal sealed class CommandPaletteConf : Notifier, IControlled<ICommandPaletteConfController> {
        private Dictionary<IGesture, Func<bool>> GestureAction =>
            _GestureAction ?? (
            _GestureAction = new() {
                { new PressGesture(PressKey.Escape), Escape },
                { new PressGesture(PressKey.Up), Up }
            });
        private Dictionary<IGesture, Func<bool>> _GestureAction;

        private void Controller_Gesture(object sender, GestureEventArgs e) {
            if (e != null) {
                var act = GestureAction.TryGetValue(e.Gesture, out var action);
                if (act) {
                    var triggered = action();
                    if (triggered) {
                        e.Triggered = true;
                    }
                }
            }
        }

        private bool Escape() {
            Escaped?.Invoke(this, EventArgs.Empty);
            return true;
        }

        private bool Up() {
            var caretLine = Controller?.CaretLine();
            if (caretLine == 0) {
                EscapedUp?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return false;
        }

        public event EventHandler Escaped;
        public event EventHandler EscapedUp;
        public event EventHandler TextChanged;

        public ICommandContextConf Suggestion {
            get => _Suggestion;
            set {
                if (Change(ref _Suggestion, value, nameof(Suggestion))) {
                    Text = _Suggestion?.Text;
                }
            }
        }
        private ICommandContextConf _Suggestion;

        public string Text {
            get => _Text;
            set {
                if (Change(ref _Text, value, nameof(Text))) {
                    TextChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        private string _Text;

        public void Focus() {
            Controller?.Focus();
        }

        ICommandPaletteConfController IControlled<ICommandPaletteConfController>.Controller {
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
        private ICommandPaletteConfController Controller;
    }
}
