using Brows.Localization;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Triggers;

internal sealed class PressGestureTranslation {
    private readonly Dictionary<PressGesture, PressGestureDisplay> PressGestureDisplays = [];

    public char Delimiter => _Delimiter ??=
        (Translation.Value($"{nameof(PressGesture)}_{nameof(Delimiter)}")?.FirstOrDefault(defaultValue: '+')
            ?? '+');
    private char? _Delimiter;

    public IReadOnlyDictionary<PressKey, string> PressKeyTranslation => field ??=
        Enum.GetValues<PressKey>().Distinct().ToDictionary(
            pressKey => pressKey,
            pressKey => Translation.Value($"{nameof(PressKey)}_{pressKey}") ?? pressKey.ToString());

    public IReadOnlyDictionary<PressModifiers, string> PressModifiersTranslation => field ??=
        Enum.GetValues<PressModifiers>().ToDictionary(
            pressMod => pressMod,
            pressMod => Translation.Value($"{nameof(PressModifiers)}_{pressMod}") ?? pressMod.ToString());

    public IReadOnlyDictionary<string, PressKey> PressKeyLookup => field ??=
        PressKeyTranslation.ToDictionary(
            pair => pair.Value,
            pair => pair.Key,
            StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, PressModifiers> PressModifiersLookup => field ??=
        PressModifiersTranslation.ToDictionary(
            pair => pair.Value,
            pair => pair.Key,
            StringComparer.OrdinalIgnoreCase);

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
