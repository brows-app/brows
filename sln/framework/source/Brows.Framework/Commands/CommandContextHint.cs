using Domore.Notification;

namespace Brows.Commands;

/// <summary>
/// Identifies the command associated with a palette hint.
/// </summary>
public class CommandContextHint : Notifier, ICommandContextHint {
    /// <summary>
    /// Gets the command associated with the hint.
    /// </summary>
    public ICommand Command { get; }

    /// <summary>
    /// Initializes a palette hint for a command.
    /// </summary>
    /// <param name="command">
    /// The command associated with the hint.
    /// </param>
    public CommandContextHint(ICommand command) {
        Command = command;
    }
}
