using Brows.Panels;
using Domore.Notification;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.ExceptionServices;

namespace Brows.Entries;

internal sealed class EntryObservationSource : Notifier {
    private readonly HashSet<IEntry> Select = [];
    private readonly ObservableCollection<IEntry> Collection = [];

    private IReadOnlyList<IEntry> Snapshot {
        set;
        get {
            if (field is null) {
                field = [.. Collection];
            }
            return field;
        }
    }

    private void SelectChanged() {
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Entry_Drop(object sender, EntryDropEventArgs e) {
        if (e != null) {
            Drop = e.Drop;
            Drop = null;
        }
    }

    private void Entry_Selected(object sender, EntrySelectedEventArgs e) {
        Selected(sender as IEntry);
    }

    private void Selected(IEntry entry) {
        if (entry != null) {
            if (entry.Select) {
                if (Select.Add(entry)) {
                    SelectChanged();
                }
            }
            else {
                if (Select.Remove(entry)) {
                    SelectChanged();
                }
            }
        }
    }

    private void Initialize(IEntry entry) {
        if (null == entry) throw new ArgumentNullException(nameof(entry));
        Exception error = null;
        try {
            entry.Drop += Entry_Drop;
        }
        catch (Exception ex) {
            error = ex;
        }
        try {
            entry.Selected += Entry_Selected;
        }
        catch (Exception ex) {
            error ??= ex;
        }
        try {
            Selected(entry);
        }
        catch (Exception ex) {
            error ??= ex;
        }
        if (error is not null) {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    private void Finalize(IEntry entry) {
        if (null == entry) throw new ArgumentNullException(nameof(entry));
        var selected = Select.Remove(entry);
        var dropped = Drop == entry;
        Exception error = null;
        try {
            entry.Drop -= Entry_Drop;
        }
        catch (Exception ex) {
            error = ex;
        }
        try {
            entry.Selected -= Entry_Selected;
        }
        catch (Exception ex) {
            error ??= ex;
        }
        if (selected) {
            try {
                SelectChanged();
            }
            catch (Exception ex) {
                error ??= ex;
            }
        }
        if (dropped) {
            try {
                Drop = null;
            }
            catch (Exception ex) {
                error ??= ex;
            }
        }
        if (error is not null) {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    private void PrivateAdd(IEntry entry) {
        Snapshot = null;
        Exception error = null;
        try {
            Collection.Add(entry);
        }
        catch (Exception ex) {
            error = ex;
        }
        try {
            Initialize(entry);
        }
        catch (Exception ex) {
            error ??= ex;
        }
        if (error is not null) {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    public event EventHandler DropChanged;
    public event EventHandler SelectionChanged;

    public IPanelDrop Drop {
        get;
        private set {
            if (Change(ref field, value)) {
                DropChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public IEnumerable Items =>
        Collection;

    public IReadOnlyList<IEntry> Observation =>
        Snapshot;

    public IReadOnlySet<IEntry> Selection =>
        Select;

    public void Add(IEntry entry) {
        PrivateAdd(entry);
    }

    public void Add(params IEntry[] entries) {
        ArgumentNullException.ThrowIfNull(entries);
        Exception error = null;
        foreach (var entry in entries) {
            try {
                PrivateAdd(entry);
            }
            catch (Exception ex) {
                error ??= ex;
            }
        }
        if (error is not null) {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    public bool Remove(IEntry entry) {
        Exception error = null;
        try {
            Finalize(entry);
        }
        catch (Exception ex) {
            error = ex;
        }
        Snapshot = null;
        var removed = false;
        try {
            removed = Collection.Remove(entry);
        }
        catch (Exception ex) {
            error ??= ex;
        }
        if (error is not null) {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
        return removed;
    }

    public void Clear() {
        Exception error = null;
        foreach (var entry in Collection.ToArray()) {
            try {
                Finalize(entry);
            }
            catch (Exception ex) {
                error ??= ex;
            }
        }
        Snapshot = null;
        try {
            Collection.Clear();
        }
        catch (Exception ex) {
            error ??= ex;
        }
        if (error is not null) {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    public void End() {
        DropChanged = null;
        SelectionChanged = null;
    }
}
