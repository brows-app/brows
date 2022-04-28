namespace Brows {
    internal class CommanderManualData : CommandContextData, ICommandContextHint {
        private readonly CommanderManual Agent = new CommanderManual();

        public override object Current => Agent;

        public CommanderManualData(ICommand command) : base(command) {
        }
    }
}
