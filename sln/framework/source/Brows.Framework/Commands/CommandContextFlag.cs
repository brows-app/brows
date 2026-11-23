namespace Brows.Commands;

/// <summary>
/// Specifies how the command palette retains and refreshes input after execution.
/// </summary>
public class CommandContextFlag : ICommandContextFlag {
    /// <summary>
    /// Gets or sets whether to keep the palette input open after the command runs.
    /// </summary>
    public bool PersistInput { get; set; }

    /// <summary>
    /// Gets or sets whether to refresh retained input and apply the replacement text and selection.
    /// </summary>
    public bool RefreshInput { get; set; }

    /// <summary>
    /// Gets or sets the zero-based start of the selection applied when retained input is refreshed.
    /// </summary>
    public int SelectInputStart { get; set; }

    /// <summary>
    /// Gets or sets the number of characters to select when retained input is refreshed.
    /// </summary>
    public int SelectInputLength { get; set; }

    /// <summary>
    /// Gets or sets the replacement text for refreshed input, or null to retain its text.
    /// </summary>
    public string SetInput { get; set; }
}
