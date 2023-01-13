using System.Collections.Generic;

namespace Brows {
    public interface ICommandCollection : IEnumerable<ICommand> {
        bool Triggered(string input, out IReadOnlySet<ICommand> commands);
        bool Triggered(PressGesture press, out IReadOnlySet<ICommand> commands);
        bool Arbitrary(out IReadOnlySet<ICommand> commands);
    }
}
