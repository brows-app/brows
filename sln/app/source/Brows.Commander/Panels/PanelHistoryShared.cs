using System.Collections.Generic;

namespace Brows.Panels;

internal sealed class PanelHistoryShared {
    private readonly HashSet<string> Set = new();

    public IReadOnlyCollection<string> Values =>
        Set;

    public void Add(string id) {
        Set.Add(id);
    }
}
