using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Collections {
    internal class EntrySort {
        public const int Ascending = +1;
        public const int Descending = -1;

        private EntrySortIndexCollection Indexes =>
            _Indexes ?? (
            _Indexes = new EntrySortIndexCollection());
        private EntrySortIndexCollection _Indexes;

        public IComparer Comparer =>
            _Comparer ?? (
            _Comparer = new EntryComparer(Indexes));
        private IComparer _Comparer;

        public IEnumerable<string> Keys => Indexes.Select(item => item.Key).ToList();
        public bool Comparing => Indexes.Count > 0;

        public void Clear() {
            Indexes.Clear();
        }

        public void Cycle(string key) {
            if (Indexes.Contains(key)) {
                var index = Indexes[key];
                if (index.Multiplier == Ascending) {
                    index.Multiplier = Descending;
                    return;
                }
                Indexes.Remove(key);
                return;
            }
            Indexes.Add(new EntrySortIndex(key) { Multiplier = Ascending });
        }

        public int Multiplier(string key) {
            return Indexes.TryGetValue(key, out var index)
                ? index.Multiplier
                : 0;
        }
    }
}
