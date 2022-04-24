using System;
using System.Collections;
using System.Collections.Generic;

namespace Brows.Collections {
    internal class EntryComparer : IComparer {
        public IEnumerable<EntrySortIndex> Indexes { get; }

        public EntryComparer(IEnumerable<EntrySortIndex> indexes) {
            Indexes = indexes ?? throw new ArgumentNullException(nameof(indexes));
        }

        int IComparer.Compare(object x, object y) {
            if (x is IEntry xEntry && y is IEntry yEntry) {
                foreach (var index in Indexes) {
                    int compare() {
                        var key = index.Key;
                        var yData = yEntry[key]?.Value;
                        var xData = xEntry[key]?.Value;
                        if (xData is IComparable xComp) {
                            try {
                                return xComp.CompareTo(yData);
                            }
                            catch (ArgumentException) {
                            }
                        }
                        return 0;
                    }
                    var result = compare();
                    if (result != 0) {
                        return result * index.Multiplier;
                    }
                }
            }
            return 0;
        }
    }
}
