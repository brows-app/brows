using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Gui {
    internal interface IEntryObservationController {
        event EventHandler ManualInteraction;
        event EventHandler NothingSelected;
        event EventHandler DraggingSelected;
        event EventHandler CurrentEntryChanged;
        object DraggingSource { get; }
        void Sort(IEntrySorting sorting);
        bool Focus();
        bool Focused();
        bool AddData(IEntryDataDefinition definition);
        bool HasData(string key);
        bool RemoveData(string key);
        void ClearData();
        bool SizeToFit(string key);
        Task<bool> CurrentEntry(IEntry item, CancellationToken token);
        bool CurrentEntry(IEntry item);
        IEntry CurrentEntry();
    }
}
