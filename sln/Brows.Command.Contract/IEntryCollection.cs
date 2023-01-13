using System.Collections.Generic;

namespace Brows {
    public interface IEntryCollection : IReadOnlyList<IEntry> {
        bool HasColumn(string name);
        void ClearColumns();
        void RefreshColumns();
        string[] SortColumns(IEntrySorting sorting);
        string[] AddColumns(params string[] names);
        string[] RemoveColumns(params string[] names);
    }
}
