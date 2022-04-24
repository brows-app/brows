using System;
using System.Linq;

namespace Brows.Triggers {
    public struct KeyboardGesture {
        public KeyboardKey Key { get; }
        public KeyboardModifiers Modifiers { get; }

        public KeyboardGesture(KeyboardKey key, KeyboardModifiers modifiers) {
            Key = key;
            Modifiers = modifiers;
        }

        public KeyboardGesture(KeyboardKey key) : this(key, KeyboardModifiers.None) {
        }

        public override string ToString() {
            var s = "";
            foreach (var value in Enum.GetValues(typeof(KeyboardModifiers)).Cast<KeyboardModifiers>()) {
                if (value != KeyboardModifiers.None) {
                    if (Modifiers.HasFlag(value)) {
                        s += $"{value}+";
                    }
                }
            }
            if (Key != KeyboardKey.None) {
                s += Key;
            }
            else {
                s = s.TrimEnd('+');
            }
            return s;
        }

        public override bool Equals(object obj) {
            return
                obj is KeyboardGesture other &&
                other.Key.Equals(Key) &&
                other.Modifiers.Equals(Modifiers);
        }

        public override int GetHashCode() {
            unchecked {
                var
                hashCode = (int)2166136261;
                hashCode = (hashCode * 16777619) ^ Key.GetHashCode();
                hashCode = (hashCode * 16777619) ^ Modifiers.GetHashCode();
                return hashCode;
            }
        }
    }
}
