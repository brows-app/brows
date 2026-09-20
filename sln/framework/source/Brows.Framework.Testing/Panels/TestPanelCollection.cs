using System.Collections.Generic;
using System.Linq;

namespace Brows.Panels;

public sealed class TestPanelCollection : IPanelCollection {
    /// <summary>
    /// The panels of the collection.
    /// </summary>
    public IList<IPanel> Panels { get; } = [];

    /// <summary>
    /// The active panel. Defaults to the first panel of <see cref="Panels"/>.
    /// </summary>
    public IPanel Active {
        get => field ?? Panels.FirstOrDefault();
        set;
    }

    /// <summary>
    /// The passive panel. Defaults to the first panel of <see cref="Panels"/>
    /// that is not <see cref="Active"/>.
    /// </summary>
    public IPanel Passive {
        get => field ?? Panels.FirstOrDefault(panel => panel != Active);
        set;
    }

    /// <summary>
    /// The history shared by the panels of the collection.
    /// </summary>
    public ICollection<string> History { get; } = [];

    public IPanel this[int index] => Panels[index];

    public int Count => Panels.Count;

    IReadOnlyCollection<string> IPanelCollection.History =>
        History.ToList();

    IEnumerable<IPanel> IPanelCollection.AsEnumerable() {
        return Panels.AsEnumerable();
    }

    IEnumerator<IPanel> IPanelCollection.GetEnumerator() {
        return Panels.GetEnumerator();
    }

    bool IPanelCollection.HasColumn(int column, out IPanel panel) {
        panel = Panels.FirstOrDefault(item => item.Column == column);
        return panel != null;
    }
}
