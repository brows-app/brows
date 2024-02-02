using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IEntryObservation {
        event EventHandler DropChanged;
        event EventHandler SortingChanged;
        event EventHandler CurrentChanged;
        event EventHandler SelectedChanged;
        event EventHandler ObservedChanged;
        event EventHandler ControllerChanged;
        bool ManualInteraction { get; }
        IEntryDataView DataView { get; }
        IEntry LookupID(string value);
        IEntry LookupName(string value);
        IPanelDrop Drop { get; }
        IReadOnlySet<IEntry> Selected { get; }
        IReadOnlyList<IEntry> Sorting { get; }
        IReadOnlyList<IEntry> Observed { get; }
        bool Focus();
        bool Current(IEntry entry);
        Task<bool> Current(IEntry entry, CancellationToken token);
        IEntry Current();
    }
}
