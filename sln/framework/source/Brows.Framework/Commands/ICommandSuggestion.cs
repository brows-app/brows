namespace Brows.Commands;

/// <summary>
/// Describes a command palette suggestion, its display text, and its ranking.
/// </summary>
public interface ICommandSuggestion {
    /// <summary>
    /// Gets the input inserted when the suggestion is chosen.
    /// </summary>
    string Input { get; }

    /// <summary>
    /// Gets the display group for the suggestion.
    /// </summary>
    string Group { get; }

    /// <summary>
    /// Gets the usage help displayed for the suggestion.
    /// </summary>
    string Help { get; }

    /// <summary>
    /// Gets the text describing the suggested action.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the display text for keyboard shortcuts associated with the suggestion.
    /// </summary>
    string Press { get; }

    /// <summary>
    /// Gets the display text for alternative input names.
    /// </summary>
    string Alias { get; }

    /// <summary>
    /// Gets whether the suggestion comes from command history.
    /// </summary>
    bool History { get; }

    /// <summary>
    /// Gets the score used to rank the suggestion.
    /// </summary>
    int Relevance { get; }

    /// <summary>
    /// Gets the order used to arrange the suggestion's group.
    /// </summary>
    int GroupOrder { get; }
}
