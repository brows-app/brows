using System;

namespace Brows {
    public sealed class InputEventArgs : EventArgs {
        public bool Triggered { get; set; }
        public string Text { get; }

        public InputEventArgs(string text) {
            Text = text;
        }
    }
}
