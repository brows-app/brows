using System;
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
            return new GestureEventArgs(new PressGesture(
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
    }
}
