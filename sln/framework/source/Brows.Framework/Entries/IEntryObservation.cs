using Brows.Panels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Brows.Entries;

/// <summary>
/// Provides an entry collection's selection, current item, drop state, and view controls.
/// </summary>
public interface IEntryObservation {
    /// <summary>
    /// Occurs when the drop information changes.
    /// </summary>
    event EventHandler DropChanged;

    /// <summary>
    /// Occurs when the entries waiting for sort values change.
    /// </summary>
    event EventHandler SortingChanged;

    /// <summary>
    /// Occurs when the current entry changes.
    /// </summary>
    event EventHandler CurrentChanged;

    /// <summary>
    /// Occurs when the selected entries change.
    /// </summary>
    event EventHandler SelectedChanged;

    /// <summary>
    /// Occurs when the observed entry collection changes.
    /// </summary>
    event EventHandler ObservedChanged;

    /// <summary>
    /// Occurs when the entry view controller changes.
    /// </summary>
    event EventHandler ControllerChanged;

    /// <summary>
    /// Gets whether the user has interacted with the entry view.
    /// </summary>
    bool ManualInteraction { get; }

    /// <summary>
    /// Gets the column and sorting controls for the entry view.
    /// </summary>
    IEntryDataView DataView { get; }

    /// <summary>
    /// Finds an observed entry by identifier.
    /// </summary>
    /// <param name="value">
    /// The entry identifier to find.
    /// </param>
    /// <returns>
    /// The matching entry, or null if no entry is found.
    /// </returns>
    IEntry LookupID(string value);

    /// <summary>
    /// Finds an observed entry by name.
    /// </summary>
    /// <param name="value">
    /// The entry name to find.
    /// </param>
    /// <returns>
    /// The matching entry, or null if no entry is found.
    /// </returns>
    IEntry LookupName(string value);

    /// <summary>
    /// Gets the current drop information.
    /// </summary>
    IPanelDrop Drop { get; }

    /// <summary>
    /// Gets the selected entries.
    /// </summary>
    IReadOnlySet<IEntry> Selected { get; }

    /// <summary>
    /// Gets the entries waiting for the data needed to sort them.
    /// </summary>
    IReadOnlyList<IEntry> Sorting { get; }

    /// <summary>
    /// Gets the entries currently included in the observation.
    /// </summary>
    IReadOnlyList<IEntry> Observed { get; }

    /// <summary>
    /// Requests keyboard focus for the entry view.
    /// </summary>
    /// <returns>
    /// True if the view was focused; otherwise, false.
    /// </returns>
    bool Focus();

    /// <summary>
    /// Makes an entry current in the view.
    /// </summary>
    /// <param name="entry">
    /// The entry to make current.
    /// </param>
    /// <returns>
    /// True if the entry was made current; otherwise, false.
    /// </returns>
    bool Current(IEntry entry);

    /// <summary>
    /// Makes an entry current in the view.
    /// </summary>
    /// <param name="entry">
    /// The entry to make current.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the entry was made current; otherwise, false.
    /// </returns>
    Task<bool> Current(IEntry entry, CancellationToken token);

    /// <summary>
    /// Gets the current entry in the view.
    /// </summary>
    /// <returns>
    /// The current entry, or null when none is current.
    /// </returns>
    IEntry Current();
}
