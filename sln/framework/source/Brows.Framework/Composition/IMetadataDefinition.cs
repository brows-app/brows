namespace Brows.Composition;

/// <summary>
/// Identifies a file property and its display name.
/// </summary>
public interface IMetadataDefinition {
    /// <summary>
    /// Gets the key that identifies the property.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Gets the property's display name.
    /// </summary>
    string Name { get; }
}
