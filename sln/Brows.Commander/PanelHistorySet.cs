using System.Collections.Generic;

namespace Brows {
    internal sealed class PanelHistorySet {
        private readonly Dictionary<string, PanelHistoryItem> Dict = new();

        public PanelHistoryItem Get(string value) {
            return Dict.TryGetValue(value, out var item)
                ? item
                : null;
        }

        public PanelHistoryItem GetOrAdd(string id) {
            var dict = Dict;
            if (dict.TryGetValue(id, out var item) == false) {
                dict[id] = item = new PanelHistoryItem(id);
            }
            return item;
        }
    }
}
