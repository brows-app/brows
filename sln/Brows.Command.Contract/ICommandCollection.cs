using System.Collections.Generic;

namespace Brows {
    public interface ICommandCollection : IEnumerable<ICommand> {
        bool Triggered(ITrigger trigger, out IReadOnlySet<ICommand> commands);
        bool Arbitrary(out IReadOnlySet<ICommand> commands);
    }
}
