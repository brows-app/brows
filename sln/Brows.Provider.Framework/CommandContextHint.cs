using Domore.Notification;

namespace Brows {
    public class CommandContextHint : Notifier, ICommandContextHint {
        public ICommand Command { get; }

        public CommandContextHint(ICommand command) {
            Command = command;
        }
    }
}
