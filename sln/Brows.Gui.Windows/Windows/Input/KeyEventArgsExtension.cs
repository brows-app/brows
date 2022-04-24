using System;
using System.Windows.Input;
using MODIFIERKEYS = System.Windows.Input.ModifierKeys;

namespace Brows.Windows.Input {
    public static class KeyEventArgsExtension {
        public static MODIFIERKEYS ModifierKeys(this KeyEventArgs keyEventArgs) {
            if (null == keyEventArgs) throw new ArgumentNullException(nameof(keyEventArgs));
            return keyEventArgs.KeyboardDevice?.Modifiers ?? MODIFIERKEYS.None;
        }

        public static Key ReferencedKey(this KeyEventArgs keyEventArgs) {
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
    }
}
