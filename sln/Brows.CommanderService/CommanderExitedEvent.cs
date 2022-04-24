using System;

namespace Brows {
    public delegate void CommanderExitedEventHandler(object sender, CommanderExitedEventArgs e);

    public class CommanderExitedEventArgs : EventArgs {
    }
}
