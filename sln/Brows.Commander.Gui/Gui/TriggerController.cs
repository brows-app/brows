using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Brows.Gui {
    using Triggers;
    using Windows.Input;

    internal class TriggerController<T> : Controller<T> where T : class {
        private Window Window;

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (e != null) {
                var key = (KeyboardKey)e.ReferencedKey();
                var modifiers = (KeyboardModifiers)e.ModifierKeys();
                var eventArgs = new KeyboardKeyEventArgs(key, modifiers);
                WindowKeyboardKeyDown?.Invoke(this, eventArgs);
                var handled = e.Handled = eventArgs.Triggered;
                if (handled == false) {
                    if (key == KeyboardKey.Space) {
                        var input = new InputEventArgs(" ");
                        WindowInput?.Invoke(this, input);
                        e.Handled = input.Triggered;
                    }
                }
            }
        }

        private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e) {
            if (e != null) {
                var text = e.Text;
                var args = new InputEventArgs(text);
                WindowInput?.Invoke(this, args);
                e.Handled = args.Triggered;
            }
        }

        protected override void OnLoaded(EventArgs e) {
            Window = Window.GetWindow(UserControl);
            Window.PreviewKeyDown += Window_PreviewKeyDown;
            Window.PreviewTextInput += Window_PreviewTextInput;
            base.OnLoaded(e);
        }

        protected override void OnUnloaded(EventArgs e) {
            var window = Window;
            if (window != null) {
                window.PreviewKeyDown -= Window_PreviewKeyDown;
                window.PreviewTextInput -= Window_PreviewTextInput;
            }
            Window = null;
            base.OnUnloaded(e);
        }

        public event InputEventHandler WindowInput;
        public event KeyboardKeyEventHandler WindowKeyboardKeyDown;

        public TriggerController(UserControl userControl) : base(userControl) {
        }
    }
}
