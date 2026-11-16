using Brows.Localization;
using System;

namespace Brows;

/// <summary>
/// Reads the localized texts of the feature.
/// </summary>
/// <remarks>
/// The translation of the application is not available when the feature is used outside of it,
/// so a text that has no translation falls back to the key of the text, and never to an error.
/// </remarks>
internal static class MBoxText {
    /// <summary>
    /// Returns the localized text of the specified key, or the key itself when the text has no
    /// translation.
    /// </summary>
    /// <param name="key">
    /// The key of the text.
    /// </param>
    /// <returns>
    /// The localized text of the specified key.
    /// </returns>
    public static string Value(string key) {
        if (string.IsNullOrEmpty(key)) {
            return string.Empty;
        }
        var translate = Translation.Global;
        if (translate == null) {
            return key;
        }
        var value = translate.Value(key);
        return string.IsNullOrEmpty(value) ? key : value;
    }
}
