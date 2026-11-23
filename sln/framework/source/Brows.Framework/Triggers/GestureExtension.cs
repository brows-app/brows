using Brows.Localization;
using System.Runtime.CompilerServices;

namespace Brows.Triggers;

/// <summary>
/// Formats and parses keyboard and mouse gestures using application translations.
/// </summary>
public static class GestureExtension {
    private static readonly ConditionalWeakTable<ITranslation, ClickGestureTranslation> ClickTranslations = new();
    private static readonly ConditionalWeakTable<ITranslation, PressGestureTranslation> PressTranslations = new();

    private static ClickGestureTranslation ClickTranslation(ITranslation translation) {
        ArgumentNullException.ThrowIfNull(translation);
        return ClickTranslations.GetValue(translation, item => new ClickGestureTranslation(item));
    }

    private static PressGestureTranslation PressTranslation(ITranslation translation) {
        ArgumentNullException.ThrowIfNull(translation);
        return PressTranslations.GetValue(translation, item => new PressGestureTranslation(item));
    }

    /// <summary>
    /// Formats a supported keyboard or mouse gesture for display.
    /// </summary>
    /// <param name="gesture">
    /// The keyboard or mouse gesture to format.
    /// </param>
    /// <param name="translation">
    /// The translation used to format the gesture.
    /// </param>
    /// <returns>
    /// The translated gesture text.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="translation"/> is null.
    /// </exception>
    public static string Display(this IGesture gesture, ITranslation translation) {
        if (gesture is PressGesture press) {
            return PressTranslation(translation).Display(press).Display;
        }
        if (gesture is ClickGesture click) {
            return ClickTranslation(translation).Display(click).Display;
        }
        throw new InvalidGestureArgumentException(gesture);
    }

    /// <summary>
    /// Parses text as the same kind of gesture as the supplied gesture.
    /// </summary>
    /// <param name="gesture">
    /// A keyboard or mouse gesture specifying which kind to parse.
    /// </param>
    /// <param name="s">
    /// The gesture text to parse.
    /// </param>
    /// <param name="translation">
    /// The translation used to parse the gesture text.
    /// </param>
    /// <returns>
    /// The parsed keyboard or mouse gesture.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="translation"/> is null.
    /// </exception>
    public static IGesture Parse(this IGesture gesture, string s, ITranslation translation) {
        if (gesture is PressGesture) {
            return PressTranslation(translation).Parse(s).Gesture;
        }
        if (gesture is ClickGesture) {
            return ClickTranslation(translation).Parse(s).Gesture;
        }
        throw new InvalidGestureArgumentException(gesture);
    }

    /// <summary>
    /// Gets the character separating parts of a displayed gesture.
    /// </summary>
    /// <param name="gesture">
    /// The keyboard or mouse gesture whose delimiter is requested.
    /// </param>
    /// <param name="translation">
    /// The translation used to format the gesture.
    /// </param>
    /// <returns>
    /// The delimiter for the gesture kind.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="translation"/> is null.
    /// </exception>
    public static char Delimiter(this IGesture gesture, ITranslation translation) {
        if (gesture is PressGesture press) {
            return PressTranslation(translation).Display(press).Delimiter;
        }
        if (gesture is ClickGesture click) {
            return ClickTranslation(translation).Display(click).Delimiter;
        }
        throw new InvalidGestureArgumentException(gesture);
    }

    private sealed class InvalidGestureArgumentException : ArgumentException {
        public IGesture Gesture { get; }

        public InvalidGestureArgumentException(IGesture gesture) {
            Gesture = gesture;
        }
    }
}
