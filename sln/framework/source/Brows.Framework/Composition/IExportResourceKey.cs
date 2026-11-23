namespace Brows.Composition;

/// <summary>
/// Builds resource keys for an exported feature.
/// </summary>
public interface IExportResourceKey {
    /// <summary>
    /// Builds the feature-specific resource key for a name.
    /// </summary>
    /// <param name="key">
    /// The resource name within the feature.
    /// </param>
    /// <returns>
    /// The resource key.
    /// </returns>
    public string For(string key);
}
