using System;
using System.ComponentModel;

namespace Brows.Extensions {
    public static class EntrySortDirectionExtension {
        public static ListSortDirection ToListSortDirection(this EntrySortDirection entrySortDirection) {
            switch (entrySortDirection) {
                case EntrySortDirection.Ascending:
                    return ListSortDirection.Ascending;
                case EntrySortDirection.Descending:
                    return ListSortDirection.Descending;
                default:
                    throw new NotImplementedException();
            }
        }

        public static ListSortDirection? ToListSortDirection(this EntrySortDirection? entrySortDirection) {
            return entrySortDirection.HasValue
                ? entrySortDirection.Value.ToListSortDirection()
                : null;
        }
    }
}
