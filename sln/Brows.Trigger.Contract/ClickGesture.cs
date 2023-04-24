using System;

namespace Brows {
    public struct ClickGesture : IGesture, IEquatable<ClickGesture> {
        private int? GotHashCode;

        public int Clicks { get; }
        public ClickButton Button { get; }
        public ClickModifiers Modifiers { get; }

        public ClickGesture(ClickButton button, ClickModifiers modifiers, int clicks) {
            Clicks = clicks;
            Button = button;
            Modifiers = modifiers;
        }

        public ClickGesture(ClickButton button, int count) : this(button, ClickModifiers.None, count) {
        }

        public bool Equals(ClickGesture other) {
            return
                other.Clicks.Equals(Clicks) &&
                other.Button.Equals(Button) &&
                other.Modifiers.Equals(Modifiers);
        }

        public override bool Equals(object obj) {
            return obj is ClickGesture other && Equals(other);
        }

        public bool Equals(IGesture other) {
            return Equals((object)other);
        }

        public override int GetHashCode() {
            return GotHashCode ??= HashCode.Combine(Clicks, Button, Modifiers);
        }

        public static bool operator ==(ClickGesture a, ClickGesture b) {
            return
                a.Clicks == b.Clicks &&
                a.Button == b.Button &&
                a.Modifiers == b.Modifiers;
        }

        public static bool operator !=(ClickGesture a, ClickGesture b) {
            return
                a.Clicks != b.Clicks ||
                a.Button != b.Button ||
                a.Modifiers != b.Modifiers;
        }
    }
}
