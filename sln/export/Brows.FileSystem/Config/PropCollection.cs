using System;
using System.Collections;
using System.Collections.Generic;

namespace Brows.Config {
    internal class PropCollection : IEnumerable<PropItem> {
        private readonly HashSet<string> Key = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PropItem> Set = new(StringComparer.OrdinalIgnoreCase);

        public PropItem this[string key] {
            get {
                if (Set.TryGetValue(key, out var item) == false) {
                    Set[key] = item = new PropItem(key);
                    Key.Add(key);
                }
                return item;
            }
        }

        public IReadOnlySet<string> Keys =>
            Key;

        public IEnumerator<PropItem> GetEnumerator() {
            return ((IEnumerable<PropItem>)Set.Values).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return ((IEnumerable)Set.Values).GetEnumerator();
        }
    }
}
