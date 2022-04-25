using System.Collections.Generic;

namespace Brows {
    using Diagnostics;

    internal class CommandProcessData : CommandContextData<ProcessWrapper>, ICommandContextHint {
        protected override CommandContextData<ProcessWrapper> Create(int index, IList<ProcessWrapper> list) {
            return new CommandProcessData(Command, index, list);
        }

        public CommandProcessData(ICommand command, int index, IList<ProcessWrapper> list) : base(command, index, list) {
        }
    }
}
