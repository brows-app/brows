namespace Brows.Commands;

/// <summary>
/// Pairs a command with configuration text for the palette.
/// </summary>
public sealed class CommandContextConf : ICommandContextConf {
    /// <summary>
    /// Gets the configuration text shown in the palette.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the command associated with the configuration text.
    /// </summary>
    public ICommand Command { get; }

    /// <summary>
    /// Initializes configuration text for a command.
    /// </summary>
    /// <param name="command">
    /// The command associated with the text.
    /// </param>
    /// <param name="text">
    /// The configuration text.
    /// </param>
    public CommandContextConf(ICommand command, string text) {
        Command = command;
        Text = text;
    }
}
