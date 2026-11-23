using System.Collections.Generic;

namespace Brows.Commands;

/// <summary>
/// Specifies the initial input, selection, and command suggestions for a palette.
/// </summary>
public interface ICommandPaletteConfig {
    /// <summary>
    /// Gets the initial palette input.
    /// </summary>
    string Input { get; }

    /// <summary>
    /// Gets the zero-based start of the initial text selection.
    /// </summary>
    int SelectedStart { get; }

    /// <summary>
    /// Gets the number of characters initially selected.
    /// </summary>
    int SelectedLength { get; }

    /// <summary>
    /// Gets the commands to use for suggestions, or null to use the available commands.
    /// </summary>
    public IEnumerable<ICommand> SuggestCommands { get; }
}
