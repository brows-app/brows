using Domore.Notification;

namespace Brows.Commands;

public class CommandContextHint : Notifier, ICommandContextHint {
    public ICommand Command { get; }

    public CommandContextHint(ICommand command) {
        Command = command;
    }
}
