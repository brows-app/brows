namespace Brows.Entries;

/// <summary>
/// Looks up the data definitions available to a provider.
/// </summary>
public interface IEntryDataDefinitionSet {
    /// <summary>
    /// Gets the key and alias lookup for these definitions.
    /// </summary>
    IEntryDataKeySet Key { get; }

    /// <summary>
    /// Looks up a data definition by its key.
    /// </summary>
    /// <param name="key">
    /// The data definition key.
    /// </param>
    /// <returns>
    /// The definition, or null when the key is unknown.
    /// </returns>
    IEntryDataDefinition Get(string key);
}
