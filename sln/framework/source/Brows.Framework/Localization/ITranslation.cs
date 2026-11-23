namespace Brows.Localization;

/// <summary>
/// Looks up translated text and alternative names.
/// </summary>
public interface ITranslation {
    /// <summary>
    /// Looks up translated text.
    /// </summary>
    /// <param name="key">
    /// The translation key.
    /// </param>
    /// <returns>
    /// The translated text, or null when the key has no translation.
    /// </returns>
    string Value(string key);

    /// <summary>
    /// Looks up alternative names for a translation key.
    /// </summary>
    /// <param name="key">
    /// The translation key.
    /// </param>
    /// <returns>
    /// The alternative names for the key, or an empty array when none are defined.
    /// </returns>
    string[] Alias(string key);
}
