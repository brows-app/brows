namespace Brows.Triggers {
    public sealed class KeyboardTrigger : ITrigger {
        public KeyboardGesture Gesture { get; }

        public KeyboardKey Key => Gesture.Key;
        public KeyboardModifiers Modifiers => Gesture.Modifiers;

        public KeyboardTrigger(KeyboardGesture gesture) {
            Gesture = gesture;
        }

        public KeyboardTrigger(KeyboardKey key) : this(new KeyboardGesture(key)) {
        }

        public KeyboardTrigger(KeyboardKey key, KeyboardModifiers modifiers) : this(new KeyboardGesture(key, modifiers)) {
        }

        public sealed override string ToString() {
            return Gesture.ToString();
        }

        public sealed override bool Equals(object obj) {
            return
                obj is KeyboardTrigger other &&
                other.Gesture.Equals(Gesture);
        }

        public sealed override int GetHashCode() {
            return Gesture.GetHashCode();
        }
    }
}
