using System;

namespace Brows {
    public sealed class InputEventArgs : EventArgs {
        public bool Triggered { get; set; }
        public string Text { get; }
        public object Source { get; }

        public InputEventArgs(string text, object source) {
            Text = text;
            Source = source;
        }
    }
}
