using System;
using System.Collections;
using System.Collections.Generic;

namespace Brows {
    public class EntryComparer : IComparer<IEntry>, IComparer {
        private static int Compare(IEntryData x, IEntryData y) {
            return x?.Compare(y) ?? 0;
        }

        private static int Compare(IEntryData x, IEntryData y, EntrySortDirection? direction) {
            switch (direction) {
                case EntrySortDirection.Ascending:
                    return Compare(x, y);
                case EntrySortDirection.Descending:
                    return Compare(y, x);
                default:
                    return 0;
            }
        }

        public IEntrySorting Sorting { get; }

        public EntryComparer(IEntrySorting sorting) {
            Sorting = sorting ?? throw new ArgumentNullException(nameof(sorting));
        }

        public int Compare(IEntry x, IEntry y) {
            foreach (var sort in Sorting) {
                var sortKey = sort.Key;
                var xData = x?[sortKey];
                var yData = y?[sortKey];
                var compare = Compare(xData, yData, sort.Value);
                if (compare != 0) {
                    return compare;
                }
            }
            return 0;
        }

        int IComparer.Compare(object x, object y) {
            if (x is IEntry xEntry) {
                if (y is IEntry yEntry) {
                    return Compare(xEntry, yEntry);
                }
            }
            return 0;
        }
    }
}
