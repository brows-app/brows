using System;

namespace Brows {
    public delegate void EntryRefreshedEventHandler(object sender, EntryRefreshedEventArgs e);

    public sealed class EntryRefreshedEventArgs : EventArgs {
    }
}
