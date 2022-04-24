using System.Collections.Generic;

namespace Brows {
    using Diagnostics;

    internal class CommandProcessData : CommandContextData<ProcessWrapper>, ICommandContextHint {
        protected override CommandContextData<ProcessWrapper> Create(int index, IList<ProcessWrapper> list) {
            return new CommandProcessData(Command, index, list);
        }

        public override bool CanUpDown => true;

        public CommandProcessData(ICommand command, int index, IList<ProcessWrapper> list) : base(command, index, list) {
        }

        public override void Up() {
            Item?.ScrollUp();
        }

        public override void Down() {
            Item?.ScrollDown();
        }

        public override void PageUp() {
            Item?.PageUp();
        }

        public override void PageDown() {
            Item?.PageDown();
        }
    }
}
