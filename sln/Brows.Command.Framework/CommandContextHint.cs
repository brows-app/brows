namespace Brows {
    using ComponentModel;

    public class CommandContextHint : NotifyPropertyChanged, ICommandContextHint {
        public ICommand Command { get; }

        public CommandContextHint(ICommand command) {
            Command = command;
        }
    }
}
