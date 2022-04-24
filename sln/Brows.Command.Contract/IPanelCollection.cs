using System.Collections.Generic;

namespace Brows {
    public interface IPanelCollection : IReadOnlyList<IPanel> {
        IPanel Active { get; }
        IPanel Passive { get; }
        IReadOnlyCollection<string> History { get; }
    }
}
