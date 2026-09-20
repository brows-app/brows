using Brows.Panels;
using System;

namespace Brows.Entries;

public delegate void EntryDropEventHandler(object sender, EntryDropEventArgs e);

public sealed class EntryDropEventArgs : EventArgs {
    public IPanelDrop Drop { get; }

    public EntryDropEventArgs(IPanelDrop drop) {
        Drop = drop;
    }
}
