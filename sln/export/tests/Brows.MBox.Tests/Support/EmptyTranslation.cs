using Brows.Localization;

namespace Brows;

/// <summary>
/// A translation that knows no text, so that a test that needs one can have it.
/// </summary>
/// <remarks>
/// The framework reads the names of the columns of the list through the translation of the
/// application, so a test that shows the list of the messages of a mailbox needs one.
/// </remarks>
internal sealed class EmptyTranslation : ITranslation {
    /// <summary>
    /// Returns the text of the specified key, or <see langword="null"/> when the key has no
    /// text.
    /// </summary>
    /// <param name="key">
    /// The key of the text.
    /// </param>
    /// <returns>
    /// The text of the key.
    /// </returns>
    public string Value(string key) {
        return null;
    }

    /// <summary>
    /// Returns the aliases of the specified key.
    /// </summary>
    /// <param name="key">
    /// The key of the aliases.
    /// </param>
    /// <returns>
    /// The aliases of the key.
    /// </returns>
    public string[] Alias(string key) {
        return [];
    }
}
