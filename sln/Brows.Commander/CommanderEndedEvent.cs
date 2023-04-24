using System;

namespace Brows {
    public delegate void CommanderEndedEventHandler(object sender, CommanderEndedEventArgs e);

    public sealed class CommanderEndedEventArgs : EventArgs {
    }
}
