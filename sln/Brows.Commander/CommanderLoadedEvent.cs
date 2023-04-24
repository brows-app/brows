using System;

namespace Brows {
    public delegate void CommanderLoadedEventHandler(object sender, CommanderLoadedEventArgs e);

    public sealed class CommanderLoadedEventArgs : EventArgs {
        public object Commander { get; }
        public bool First { get; }

        public CommanderLoadedEventArgs(object commander, bool first) {
            Commander = commander;
            First = first;
        }
    }
}
