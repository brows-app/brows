namespace Brows {
    public sealed class InputEventArgs : TriggerEventArgs {
        public string Text { get; }

        public InputEventArgs(string text, object source) : base(source) {
            Text = text;
        }
    }
}
