using System;

namespace Brows {
    internal sealed class PressGestureDisplay {
        private string BuildDisplay() {
            var s = "";
            var d = Translation.Delimiter;
            var k = Gesture.Key;
            var m = Gesture.Modifiers;
            foreach (var mFlag in Enum.GetValues<PressModifiers>()) {
                if (mFlag != PressModifiers.None) {
                    if (m.HasFlag(mFlag)) {
                        s += $"{Translation.PressModifiersTranslation[mFlag]}{d}";
                    }
                }
            }
            if (k != PressKey.None) {
                s += Translation.PressKeyTranslation[k];
            }
            else {
                s = s.TrimEnd(d);
            }
            return s;
        }

        public string Display =>
            _Display ?? (
            _Display = BuildDisplay());
        private string _Display;

        public char Delimiter =>
            Translation.Delimiter;

        public PressGesture Gesture { get; }
        public PressGestureTranslation Translation { get; }

        public PressGestureDisplay(PressGesture gesture, PressGestureTranslation translation) {
            Gesture = gesture;
            Translation = translation ?? throw new ArgumentNullException(nameof(translation));
        }

        public static PressGestureDisplay Parse(string s, PressGestureTranslation translation) {
            if (null == s) throw new ArgumentNullException(nameof(s));
            if (null == translation) throw new ArgumentNullException(nameof(translation));
            if (string.IsNullOrWhiteSpace(s)) {
                throw new ParseException(s, ParseExceptionReason.ArgumentEmpty);
            }
            var d = translation.Delimiter;
            var key = PressKey.None;
            var mod = PressModifiers.None;
            var parts = s.Split(d, StringSplitOptions.TrimEntries);
            foreach (var p in parts) {
                if (translation.PressKeyLookup.TryGetValue(p, out var k)) {
                    if (key == PressKey.None) {
                        key = k;
                        continue;
                    }
                    throw new ParseException(s, ParseExceptionReason.KeyAlreadyExists);
                }
                if (translation.PressModifiersLookup.TryGetValue(p, out var m)) {
                    if (mod.HasFlag(m) == false) {
                        mod |= m;
                        continue;
                    }
                    throw new ParseException(s, ParseExceptionReason.ModifierDuplicated);
                }
                throw new ParseException(s, ParseExceptionReason.UnrecognizedToken);
            }
            return new PressGestureDisplay(new PressGesture(key, mod), translation);
        }

        private enum ParseExceptionReason {
            ArgumentEmpty,
            KeyAlreadyExists,
            ModifierDuplicated,
            UnrecognizedToken
        }

        private sealed class ParseException : FormatException {
            public string Argument { get; }
            public ParseExceptionReason Reason { get; }

            public ParseException(string argument, ParseExceptionReason reason) {
                Argument = argument;
                Reason = reason;
            }
        }
    }
}
