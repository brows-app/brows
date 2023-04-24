using System.Collections.Generic;

namespace Brows {
    using Diagnostics;

    internal class CommandProcessData : CommandContextData<ProcessWrapper>, ICommandContextHint {
        protected override CommandContextData<ProcessWrapper> Create(int index, IList<ProcessWrapper> list) {
            return new CommandProcessData(Command, index, list);
        }

        protected override void Removed(ProcessWrapper item) {
            if (item != null) {
                item.Kill();
            }
        }

        protected override void Cleared(IEnumerable<ProcessWrapper> items) {
            if (items != null) {
                foreach (var item in items) {
                    item.Kill();
                }
            }
        }

        public CommandProcessData(ICommand command, int index, IList<ProcessWrapper> list) : base(command, index, list) {
        }
    }
}
