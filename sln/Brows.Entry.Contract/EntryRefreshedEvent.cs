using System;

namespace Brows {
    public delegate void EntryRefreshedEventHandler(object sender, EntryRefreshedEventArgs e);

    public sealed class EntryRefreshedEventArgs : EventArgs {
        public EntryRefresh Flags { get; }

        public EntryRefreshedEventArgs(EntryRefresh flags) {
            Flags = flags;
        }
    }
}
