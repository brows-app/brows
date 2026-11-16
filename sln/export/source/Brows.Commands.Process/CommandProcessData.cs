using Brows.Diagnostics;
using System.Collections.Generic;

namespace Brows;

internal sealed class CommandProcessData : CommandContextData<IProcessProxy>, ICommandContextHint {
    protected sealed override CommandContextData<IProcessProxy> Create(int index, IList<IProcessProxy> list) {
        return new CommandProcessData(Command, index, list);
    }

    protected sealed override void Removed(IProcessProxy item) {
        if (item != null) {
            item.Kill();
        }
    }

    protected sealed override void Cleared(IEnumerable<IProcessProxy> items) {
        if (items != null) {
            foreach (var item in items) {
                item.Kill();
            }
        }
    }

    public CommandProcessData(ICommand command, int index, IList<IProcessProxy> list) : base(command, index, list) {
    }
}
