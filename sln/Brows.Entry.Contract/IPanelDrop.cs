using System.Collections.Generic;

namespace Brows {
    public interface IPanelDrop {
        IReadOnlyList<string> CopyFiles { get; }
        IReadOnlyList<string> MoveFiles { get; }
    }
}
