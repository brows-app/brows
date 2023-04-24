using System;

namespace Brows {
    internal sealed class ClickGestureDisplay {
        private string BuildDisplay() {
            var s = "";
            var d = Translation.Delimiter;
            var b = Gesture.Button;
            var m = Gesture.Modifiers;
            foreach (var mFlag in Enum.GetValues<ClickModifiers>()) {
                if (mFlag != ClickModifiers.None) {
                    if (m.HasFlag(mFlag)) {
                        s += $"{Translation.ClickModifiersTranslation[mFlag]}{d}";
                    }
                }
            }
            if (b != ClickButton.None) {
                s += Translation.ClickButtonTranslation[b];
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

        public ClickGesture Gesture { get; }
        public ClickGestureTranslation Translation { get; }

        public ClickGestureDisplay(ClickGesture gesture, ClickGestureTranslation translation) {
            Gesture = gesture;
            Translation = translation ?? throw new ArgumentNullException(nameof(translation));
        }

        public static ClickGestureDisplay Parse(string s, ClickGestureTranslation translation) {
            if (null == s) throw new ArgumentNullException(nameof(s));
            if (null == translation) throw new ArgumentNullException(nameof(translation));
            if (string.IsNullOrWhiteSpace(s)) {
                throw new ParseException(s, ParseExceptionReason.ArgumentEmpty);
            }
            var d = translation.Delimiter;
            var button = ClickButton.None;
            var mod = ClickModifiers.None;
            var parts = s.Split(d, StringSplitOptions.TrimEntries);
            var clicks = 1;
            foreach (var p in parts) {
                if (p.Length > 1 && char.IsDigit(p[0])) {
                    clicks = int.Parse($"{p[0]}");
                    if (translation.ClickButtonLookup.TryGetValue(p.Substring(1), out var bb)) {
                        button = bb;
                        continue;
                    }
                    throw new ParseException(s, ParseExceptionReason.DigitMustPrecedeClickButton);
                }
                if (translation.ClickButtonLookup.TryGetValue(p, out var b)) {
                    if (button == ClickButton.None) {
                        button = b;
                        continue;
                    }
                    throw new ParseException(s, ParseExceptionReason.ButtonAlreadyExists);
                }
                if (translation.ClickModifiersLookup.TryGetValue(p, out var m)) {
                    if (mod.HasFlag(m) == false) {
                        mod |= m;
                        continue;
                    }
                    throw new ParseException(s, ParseExceptionReason.ModifierDuplicated);
                }
                throw new ParseException(s, ParseExceptionReason.UnrecognizedToken);
            }
            return new ClickGestureDisplay(new ClickGesture(button, mod, clicks), translation);
        }

        private enum ParseExceptionReason {
            ArgumentEmpty,
            DigitMustPrecedeClickButton,
            ButtonAlreadyExists,
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
