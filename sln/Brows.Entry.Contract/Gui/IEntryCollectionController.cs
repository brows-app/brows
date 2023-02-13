using System;
using System.Collections;
using System.Collections.Generic;

namespace Brows.Gui {
    public interface IEntryCollectionController {
        event EventHandler CurrentChanged;
        event EventHandler SelectionChanged;
        int CurrentPosition { get; }
        IEntry CurrentEntry { get; }
        IReadOnlyList<IEntry> SelectedEntries { get; }
        bool Focus();
        void Removed();
        void Source(IEnumerable collection);
        bool MoveCurrentTo(IEntry entry);
        void Sort(IEntrySorting sorting);
        void AddColumn(string key, IEntryColumn column);
        bool HasColumn(string key);
        bool RemoveColumn(string key);
        void ClearColumns();
    }
}
