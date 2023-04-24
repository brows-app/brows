using System;

namespace Brows.Gui {
    public interface IEntryObservationController {
        event EventHandler ManualInteraction;
        event EventHandler NothingSelected;
        event EventHandler DraggingSelected;
        event EventHandler CurrentEntryChanged;
        object DraggingSource { get; }
        bool CurrentEntry(IEntry item);
        IEntry CurrentEntry();
        void Sort(IEntrySorting sorting);
        bool Focus();
        bool Focused();
        bool AddData(IEntryDataDefinition definition);
        bool HasData(string key);
        bool RemoveData(string key);
        void ClearData();
        IDisposable Updating();
    }
}
