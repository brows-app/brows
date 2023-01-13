using System;

namespace Brows {
    public delegate void CommanderPressEventHandler(object sender, CommanderPressEventArgs e);

    public class CommanderPressEventArgs : EventArgs {
        public PressGesture Gesture =>
            _Gesture ?? (
            _Gesture = new PressGesture(Key, Modifiers)).Value;
        private PressGesture? _Gesture;

        public PressKey Key { get; }
        public PressModifiers Modifiers { get; }
        public bool Triggered { get; set; }

        public CommanderPressEventArgs(PressKey key, PressModifiers modifiers) {
            Key = key;
            Modifiers = modifiers;
        }
    }
}
