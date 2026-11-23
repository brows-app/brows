using System.Collections;
using System.Collections.Generic;

namespace Brows.Entries;

/// <summary>
/// Compares entries using an ordered set of data keys and sort directions.
/// </summary>
public sealed class EntryComparer : IComparer<IEntry>, IComparer {
    private static int Compare(IEntryData x, IEntryData y) {
        /*
         * Null compares lower than any non-null value, so comparisons over a
         * mixed list keep a consistent order. Treating null as equal to
         * everything breaks the sort's ordering contract.
         */
        if (x is null) {
            return y is null ? 0 : -1;
        }
        return y is null ? 1 : x.Compare(y);
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

    /// <summary>
    /// Gets the data keys and directions used for comparison.
    /// </summary>
    public IEntrySorting Sorting { get; }

    /// <summary>
    /// Initializes an entry comparer with sort keys and directions.
    /// </summary>
    /// <param name="sorting">
    /// The ordered keys and directions to apply.
    /// </param>
    public EntryComparer(IEntrySorting sorting) {
        Sorting = sorting ?? throw new ArgumentNullException(nameof(sorting));
    }

    /// <summary>
    /// Compares entries by each active sort key until their values differ.
    /// </summary>
    /// <param name="x">
    /// The first entry.
    /// </param>
    /// <param name="y">
    /// The second entry.
    /// </param>
    /// <returns>
    /// A negative value if x precedes y, zero if they compare equally, or a positive value if x follows y.
    /// </returns>
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
