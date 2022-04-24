using System;

namespace Brows.Triggers {
    public delegate void KeyboardKeyEventHandler(object sender, KeyboardKeyEventArgs e);

    public class KeyboardKeyEventArgs : EventArgs {
        public KeyboardGesture Gesture =>
            _Gesture ?? (
            _Gesture = new KeyboardGesture(Key, Modifiers)).Value;
        private KeyboardGesture? _Gesture;

        public KeyboardKey Key { get; }
        public KeyboardModifiers Modifiers { get; }
        public bool Triggered { get; set; }

        public KeyboardKeyEventArgs(KeyboardKey key, KeyboardModifiers modifiers) {
            Key = key;
            Modifiers = modifiers;
        }
    }
}
