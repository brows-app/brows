using System.Collections.Generic;

namespace Brows.Commands;

/// <summary>
/// Specifies initial input, text selection, and command suggestions for a palette.
/// </summary>
public sealed class CommandPaletteConfig : ICommandPaletteConfig {
    /// <summary>
    /// Gets or sets the initial palette input.
    /// </summary>
    public string Input { get; set; }

    /// <summary>
    /// Gets or sets the zero-based start of the initial text selection.
    /// </summary>
    public int SelectedStart { get; set; }

    /// <summary>
    /// Gets or sets the number of characters initially selected.
    /// </summary>
    public int SelectedLength { get; set; }

    /// <summary>
    /// Gets or sets the commands to use for suggestions, or null to use the available commands.
    /// </summary>
    public IEnumerable<ICommand> SuggestCommands { get; set; }
}
