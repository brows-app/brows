using System.Collections.Generic;

namespace Brows {
    public interface IPanelDrop {
        object Target { get; }
        IReadOnlyList<string> CopyFiles { get; }
        IReadOnlyList<string> MoveFiles { get; }
    }
}
