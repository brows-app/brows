using System;
using System.Windows;
using System.Windows.Input;
using MODIFIERKEYS = System.Windows.Input.ModifierKeys;

namespace Brows {
    public static class TriggerPressEvent {
        private static MODIFIERKEYS ModifierKeys(this KeyEventArgs keyEventArgs) {
            if (null == keyEventArgs) throw new ArgumentNullException(nameof(keyEventArgs));
            return keyEventArgs.KeyboardDevice?.Modifiers ?? MODIFIERKEYS.None;
        }

        private static Key ReferencedKey(this KeyEventArgs keyEventArgs) {
            if (null == keyEventArgs) throw new ArgumentNullException(nameof(keyEventArgs));
            switch (keyEventArgs.Key) {
                case Key.DeadCharProcessed:
                    return keyEventArgs.DeadCharProcessedKey;
                case Key.ImeProcessed:
                    return keyEventArgs.ImeProcessedKey;
                case Key.System:
                    return keyEventArgs.SystemKey;
                default:
                    return keyEventArgs.Key;
            }
        }

        private static GestureEventArgs Gesture(this KeyEventArgs keyEventArgs) {
            if (null == keyEventArgs) throw new ArgumentNullException(nameof(keyEventArgs));
            return new GestureEventArgs(
                source: keyEventArgs.OriginalSource is FrameworkElement element
                    ? element.DataContext
                    : null,
                gesture: new PressGesture(
                    key: (PressKey)ReferencedKey(keyEventArgs),
                    modifiers: (PressModifiers)ModifierKeys(keyEventArgs)));
        }

        public static bool Trigger(this KeyEventArgs keyEventArgs, Action<GestureEventArgs> handler) {
            if (null == keyEventArgs) throw new ArgumentNullException(nameof(keyEventArgs));
            if (null != handler) {
                var args = Gesture(keyEventArgs); handler(args);
                if (args.Triggered) {
                    return keyEventArgs.Handled = true;
                }
            }
            return false;
        }

        public static bool Trigger(this MouseButtonEventArgs mouseButtonEventArgs, int clicks, Action<GestureEventArgs> handler) {
            if (null == mouseButtonEventArgs) throw new ArgumentNullException(nameof(mouseButtonEventArgs));
            if (null != handler) {
                var e = mouseButtonEventArgs;
                var source = e.OriginalSource is FrameworkElement element ? element.DataContext : null;
                var button = (ClickButton)e.ChangedButton;
                var modifiers = (ClickModifiers)Keyboard.Modifiers;
                var eventArgs = new GestureEventArgs(new ClickGesture(button, modifiers, clicks), source);
                var args = eventArgs; handler(args);
                if (args.Triggered) {
                    return mouseButtonEventArgs.Handled = true;
                }
            }
            return false;
        }
    }
}
