namespace Brows {
    internal sealed class CommandTriggerPress : ITriggerPress {
        public PressKey Key => Gesture.Key;
        public PressModifiers Modifiers => Gesture.Modifiers;

        public string Shortcut { get; }
        public PressGesture Gesture { get; }

        public CommandTriggerPress(PressGesture gesture, string shortcut) {
            Gesture = gesture;
            Shortcut = shortcut;
        }

        public sealed override string ToString() {
            return Gesture.ToString();
        }
    }
}
