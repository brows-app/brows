using System;
using System.Collections.Generic;

namespace Brows.Gui {
    public interface IEntryCollectionController {
        event EventHandler CurrentChanged;
        event EventHandler SelectionChanged;
        int CurrentPosition { get; }
        IEntry CurrentEntry { get; }
        IEnumerable<string> Columns { get; }
        IReadOnlyList<IEntry> SelectedEntries { get; }
        bool Focus();
        bool MoveCurrentTo(IEntry entry);
        void Sort(IEntrySorting sorting);
        void Removed(IEntry entry);
        void AddColumn(string key, IEntryColumn column);
        bool HasColumn(string key);
        bool RemoveColumn(string key);
        void ClearColumns();
    }
}
