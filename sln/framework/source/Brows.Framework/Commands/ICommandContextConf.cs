namespace Brows.Commands;

/// <summary>
/// Pairs a command with configuration text shown in the palette.
/// </summary>
public interface ICommandContextConf {
    /// <summary>
    /// Gets the command associated with the configuration text.
    /// </summary>
    ICommand Command { get; }

    /// <summary>
    /// Gets the configuration text shown in the palette.
    /// </summary>
    string Text { get; }
}
