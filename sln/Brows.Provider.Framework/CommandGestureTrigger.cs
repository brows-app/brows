namespace Brows {
    internal sealed class CommandGestureTrigger : IGestureTrigger {
        public string Display => Gesture.Display();
        public string Shortcut { get; }
        public IGesture Gesture { get; }

        public CommandGestureTrigger(IGesture gesture, string shortcut) {
            Gesture = gesture;
            Shortcut = shortcut;
        }
    }
}
