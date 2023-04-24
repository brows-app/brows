using System;

namespace Brows {
    public struct PressGesture : IGesture, IEquatable<PressGesture> {
        private int? GotHashCode;

        public PressKey Key { get; }
        public PressModifiers Modifiers { get; }

        public PressGesture(PressKey key, PressModifiers modifiers) {
            Key = key;
            Modifiers = modifiers;
        }

        public PressGesture(PressKey key) : this(key, PressModifiers.None) {
        }

        public bool Equals(PressGesture other) {
            return
                other.Key.Equals(Key) &&
                other.Modifiers.Equals(Modifiers);
        }

        public override bool Equals(object obj) {
            return obj is PressGesture other && Equals(other);
        }

        public bool Equals(IGesture other) {
            return Equals((object)other);
        }

        public override int GetHashCode() {
            return GotHashCode ??= HashCode.Combine(Key, Modifiers);
        }

        public static bool operator ==(PressGesture a, PressGesture b) {
            return
                a.Key == b.Key &&
                a.Modifiers == b.Modifiers;
        }

        public static bool operator !=(PressGesture a, PressGesture b) {
            return
                a.Key != b.Key ||
                a.Modifiers != b.Modifiers;
        }
    }
}
