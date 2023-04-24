using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    internal sealed class PressGestureTranslation {
        private readonly Dictionary<PressGesture, PressGestureDisplay> PressGestureDisplays = new();

        public char Delimiter =>
            _Delimiter ?? (
            _Delimiter = Translation
                .Value($"{nameof(PressGesture)}_{nameof(Delimiter)}")?
                .FirstOrDefault(defaultValue: '+') ?? '+'
            ).Value;
        private char? _Delimiter;

        public IReadOnlyDictionary<PressKey, string> PressKeyTranslation =>
            _PressKeyTranslation ?? (
            _PressKeyTranslation = Enum.GetValues<PressKey>().Distinct().ToDictionary(
                pressKey => pressKey,
                pressKey => Translation.Value($"{nameof(PressKey)}_{pressKey}") ?? pressKey.ToString()));
        private IReadOnlyDictionary<PressKey, string> _PressKeyTranslation;

        public IReadOnlyDictionary<PressModifiers, string> PressModifiersTranslation =>
            _PressModifiersTranslation ?? (
            _PressModifiersTranslation = Enum.GetValues<PressModifiers>().ToDictionary(
                pressMod => pressMod,
                pressMod => Translation.Value($"{nameof(PressModifiers)}_{pressMod}") ?? pressMod.ToString()));
        private IReadOnlyDictionary<PressModifiers, string> _PressModifiersTranslation;

        public IReadOnlyDictionary<string, PressKey> PressKeyLookup =>
            _PressKeyLookup ?? (
            _PressKeyLookup = PressKeyTranslation.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase));
        private IReadOnlyDictionary<string, PressKey> _PressKeyLookup;

        public IReadOnlyDictionary<string, PressModifiers> PressModifiersLookup =>
            _PressModifiersLookup ?? (
            _PressModifiersLookup = PressModifiersTranslation.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase));
        private IReadOnlyDictionary<string, PressModifiers> _PressModifiersLookup;

        public ITranslation Translation { get; }

        public PressGestureTranslation(ITranslation translation) {
            Translation = translation ?? throw new ArgumentNullException(nameof(translation));
        }

        public PressGestureDisplay Display(PressGesture gesture) {
            if (PressGestureDisplays.TryGetValue(gesture, out var display) == false) {
                PressGestureDisplays[gesture] = display = new PressGestureDisplay(gesture, this);
            }
            return display;
        }

        public PressGestureDisplay Parse(string s) {
            return PressGestureDisplay.Parse(s, this);
        }
    }
}
