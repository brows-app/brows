using System.Collections.Generic;

namespace Brows {
    public interface IPanelCollection {
        IPanel this[int index] { get; }
        int Count { get; }
        IPanel Active { get; }
        IPanel Passive { get; }
        IReadOnlyCollection<string> History { get; }
        bool HasColumn(int column, out IPanel panel);
        IEnumerator<IPanel> GetEnumerator();
        IEnumerable<IPanel> AsEnumerable();
    }
}
