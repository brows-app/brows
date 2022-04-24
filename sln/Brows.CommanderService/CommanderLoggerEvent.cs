using System;

namespace Brows {
    public delegate void CommanderLoggerEventHandler(object sender, CommanderLoggerEventArgs e);

    public class CommanderLoggerEventArgs : EventArgs {
    }
}
