namespace Brows.Commands;

/// <summary>
/// Controls whether command palette input remains open and how it is refreshed after execution.
/// </summary>
public interface ICommandContextFlag {
    /// <summary>
    /// Gets whether to keep the palette input open after the command runs.
    /// </summary>
    bool PersistInput { get; }

    /// <summary>
    /// Gets whether to refresh retained input and apply the replacement text and selection.
    /// </summary>
    bool RefreshInput { get; }

    /// <summary>
    /// Gets the zero-based start of the selection applied when retained input is refreshed.
    /// </summary>
    int SelectInputStart { get; }

    /// <summary>
    /// Gets the number of characters to select when retained input is refreshed.
    /// </summary>
    int SelectInputLength { get; }

    /// <summary>
    /// Gets the replacement text for refreshed input, or null to retain its text.
    /// </summary>
    string SetInput { get; }
}
