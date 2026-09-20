using System;

namespace Brows.Entries;

public delegate void EntrySelectedEventHandler(object sender, EntrySelectedEventArgs e);

public class EntrySelectedEventArgs : EventArgs {
    public bool Select { get; }

    public EntrySelectedEventArgs(bool select) {
        Select = select;
    }
}
