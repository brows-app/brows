using Brows.Localization;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Triggers;

internal sealed class ClickGestureTranslation {
    private readonly Dictionary<ClickGesture, ClickGestureDisplay> ClickGestureDisplays = new();

    public char Delimiter => _Delimiter ??=
        (Translation.Value($"{nameof(ClickGesture)}_{nameof(Delimiter)}")?.FirstOrDefault(defaultValue: '+')
            ?? '+');
    private char? _Delimiter;

    public IReadOnlyDictionary<ClickButton, string> ClickButtonTranslation => field ??=
        Enum.GetValues<ClickButton>().Distinct().ToDictionary(
            clickButton => clickButton,
            clickButton => Translation.Value($"{nameof(ClickButton)}_{clickButton}") ?? clickButton.ToString());

    public IReadOnlyDictionary<ClickModifiers, string> ClickModifiersTranslation => field ??=
        Enum.GetValues<ClickModifiers>().ToDictionary(
            clickMod => clickMod,
            clickMod => Translation.Value($"{nameof(ClickModifiers)}_{clickMod}") ?? clickMod.ToString());

    public IReadOnlyDictionary<string, ClickButton> ClickButtonLookup => field ??=
        ClickButtonTranslation.ToDictionary(
            pair => pair.Value,
            pair => pair.Key,
            StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, ClickModifiers> ClickModifiersLookup => field ??=
        ClickModifiersTranslation.ToDictionary(
            pair => pair.Value,
            pair => pair.Key,
            StringComparer.OrdinalIgnoreCase);

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
