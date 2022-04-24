using System;

namespace Brows {
    public delegate void CommanderThemedEventHandler(object sender, CommanderThemedEventArgs e);

    public class CommanderThemedEventArgs : EventArgs {
        public CommanderTheme Theme { get; }

        public CommanderThemedEventArgs(CommanderTheme theme) {
            Theme = theme;
        }
    }
}
