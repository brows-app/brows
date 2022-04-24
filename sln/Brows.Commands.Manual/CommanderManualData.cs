namespace Brows {
    using Commands;

    internal class CommanderManualData : CommandContextData, ICommandContextHint {
        private readonly CommanderManual Agent = new CommanderManual();

        public override string Control => nameof(Manual);
        public override object Current => Agent;

        public CommanderManualData(ICommand command) : base(command) {
        }

        public override void Up() {
            Agent.Controller?.ScrollUp();
        }

        public override void Down() {
            Agent.Controller?.ScrollDown();
        }

        public override void PageUp() {
            Agent.Controller?.PageUp();
        }

        public override void PageDown() {
            Agent.Controller?.PageDown();
        }
    }
}
