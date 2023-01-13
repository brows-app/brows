using System;

namespace Brows {
    public delegate void CommanderInputEventHandler(object sender, CommanderInputEventArgs e);

    public class CommanderInputEventArgs : EventArgs {
        public string Text { get; }
        public bool Triggered { get; set; }

        public CommanderInputEventArgs(string text) {
            Text = text;
        }
    }
}
