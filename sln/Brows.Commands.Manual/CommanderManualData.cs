namespace Brows {
    using Commands;

    internal class CommanderManualData : CommandContextData, ICommandContextHint {
        private readonly CommanderManual Agent = new CommanderManual();

        public override string Control => nameof(Manual);
        public override object Current => Agent;
        public override object KeyTarget => Agent.Controller?.KeyTarget;

        public CommanderManualData(ICommand command) : base(command) {
        }
    }
}
