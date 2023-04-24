namespace Brows {
    public sealed class CommandContextConf : ICommandContextConf {
        public string Text { get; }
        public ICommand Command { get; }

        public CommandContextConf(ICommand command, string text) {
            Command = command;
            Text = text;
        }
    }
}
