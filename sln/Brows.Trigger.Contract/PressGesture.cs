using System;
using System.Linq;

namespace Brows {
    public struct PressGesture {
        public PressKey Key { get; }
        public PressModifiers Modifiers { get; }

        public PressGesture(PressKey key, PressModifiers modifiers) {
            Key = key;
            Modifiers = modifiers;
        }

        public PressGesture(PressKey key) : this(key, PressModifiers.None) {
        }

        public override string ToString() {
            var s = "";
            foreach (var value in Enum.GetValues(typeof(PressModifiers)).Cast<PressModifiers>()) {
                if (value != PressModifiers.None) {
                    if (Modifiers.HasFlag(value)) {
                        s += $"{value}+";
                    }
                }
            }
            if (Key != PressKey.None) {
                s += Key;
            }
            else {
                s = s.TrimEnd('+');
            }
            return s;
        }

        public override bool Equals(object obj) {
            return
                obj is PressGesture other &&
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

        public static PressGesture Parse(string s) {
            if (s is null) throw new ArgumentNullException(nameof(s));
            if (s == string.Empty) throw new ArgumentException(paramName: nameof(s), message: $"{nameof(Parse)} {nameof(string.Empty)}");
            try {
                var key = PressKey.None;
                var mod = PressModifiers.None;
                var parts = s.Split('+', StringSplitOptions.TrimEntries);
                foreach (var p in parts) {
                    var parsed = false;
                    if (Enum.TryParse<PressKey>(p, ignoreCase: true, out var k)) {
                        if (key == PressKey.None) {
                            key = k;
                        }
                        else {
                            throw new ArgumentException();
                        }
                        parsed = true;
                    }
                    else {
                        if (Enum.TryParse<PressModifiers>(p, ignoreCase: true, out var m)) {
                            if (mod.HasFlag(m) == false) {
                                mod |= m;
                            }
                            else {
                                throw new ArgumentException();
                            }
                            parsed = true;
                        }
                    }
                    if (parsed == false) {
                        throw new ArgumentException();
                    }
                }
                return new PressGesture(key, mod);
            }
            catch (Exception ex) {
                throw new FormatException($"{nameof(Parse)} [{s}]", ex);
            }
        }
    }
}
