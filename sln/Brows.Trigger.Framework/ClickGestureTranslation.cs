using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    internal sealed class ClickGestureTranslation {
        private readonly Dictionary<ClickGesture, ClickGestureDisplay> ClickGestureDisplays = new();

        public char Delimiter =>
            _Delimiter ?? (
            _Delimiter = Translation
                .Value($"{nameof(ClickGesture)}_{nameof(Delimiter)}")?
                .FirstOrDefault(defaultValue: '+') ?? '+'
            ).Value;
        private char? _Delimiter;

        public IReadOnlyDictionary<ClickButton, string> ClickButtonTranslation =>
            _ClickButtonTranslation ?? (
            _ClickButtonTranslation = Enum.GetValues<ClickButton>().Distinct().ToDictionary(
                clickButton => clickButton,
                clickButton => Translation.Value($"{nameof(ClickButton)}_{clickButton}") ?? clickButton.ToString()));
        private IReadOnlyDictionary<ClickButton, string> _ClickButtonTranslation;

        public IReadOnlyDictionary<ClickModifiers, string> ClickModifiersTranslation =>
            _ClickModifiersTranslation ?? (
            _ClickModifiersTranslation = Enum.GetValues<ClickModifiers>().ToDictionary(
                clickMod => clickMod,
                clickMod => Translation.Value($"{nameof(ClickModifiers)}_{clickMod}") ?? clickMod.ToString()));
        private IReadOnlyDictionary<ClickModifiers, string> _ClickModifiersTranslation;

        public IReadOnlyDictionary<string, ClickButton> ClickButtonLookup =>
            _ClickButtonLookup ?? (
            _ClickButtonLookup = ClickButtonTranslation.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase));
        private IReadOnlyDictionary<string, ClickButton> _ClickButtonLookup;

        public IReadOnlyDictionary<string, ClickModifiers> ClickModifiersLookup =>
            _ClickModifiersLookup ?? (
            _ClickModifiersLookup = ClickModifiersTranslation.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase));
        private IReadOnlyDictionary<string, ClickModifiers> _ClickModifiersLookup;

        public ITranslation Translation { get; }

        public ClickGestureTranslation(ITranslation translation) {
            Translation = translation ?? throw new ArgumentNullException(nameof(translation));
        }

        public ClickGestureDisplay Display(ClickGesture gesture) {
            if (ClickGestureDisplays.TryGetValue(gesture, out var display) == false) {
                ClickGestureDisplays[gesture] = display = new ClickGestureDisplay(gesture, this);
            }
            return display;
        }

        public ClickGestureDisplay Parse(string s) {
            return ClickGestureDisplay.Parse(s, this);
        }
    }
}
