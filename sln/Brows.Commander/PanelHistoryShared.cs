using System.Collections.Generic;

namespace Brows {
    internal sealed class PanelHistoryShared {
        private readonly HashSet<string> Set = new();

        public IReadOnlyCollection<string> Values =>
            Set;

        public void Add(string id) {
            Set.Add(id);
        }
    }
}
