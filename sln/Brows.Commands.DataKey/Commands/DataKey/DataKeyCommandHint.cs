namespace Brows.Commands.DataKey {
    internal class DataKeyCommandHint : ICommandContextHint {
        public ICommand Command { get; }
        public string Help { get; }
        public string Description { get; }

        public DataKeyCommandHint(ICommand command, string help, string description) {
            Command = command;
            Help = help;
            Description = description;
        }
    }
}
