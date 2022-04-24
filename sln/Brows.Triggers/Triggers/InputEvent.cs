using System;

namespace Brows.Triggers {
    public delegate void InputEventHandler(object sender, InputEventArgs e);

    public class InputEventArgs : EventArgs {
        public string Text { get; }
        public bool Triggered { get; set; }

        public InputEventArgs(string text) {
            Text = text;
        }
    }
}
