using System;
using System.Collections.Generic;

namespace Brows {
    public interface IEntryObservation {
        event EventHandler SortingChanged;
        event EventHandler CurrentChanged;
        event EventHandler SelectedChanged;
        event EventHandler ObservedChanged;
        bool ManualInteraction { get; }
        IEntryDataView DataView { get; }
        IReadOnlySet<IEntry> Selected { get; }
        IReadOnlyList<IEntry> Sorting { get; }
        IReadOnlyList<IEntry> Observed { get; }
        IEntry Current();
        bool Current(IEntry value);
        bool Focus();
    }

    public interface IEntryObservation<TEntry> : IEntryObservation {
    }
}
