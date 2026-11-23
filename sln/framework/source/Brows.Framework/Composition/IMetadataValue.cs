namespace Brows.Composition;

/// <summary>
/// Provides a file property value and its display text.
/// </summary>
public interface IMetadataValue {
    /// <summary>
    /// Gets the property value in its original type.
    /// </summary>
    object Object { get; }

    /// <summary>
    /// Gets the text used to display the value.
    /// </summary>
    string Display { get; }
}
