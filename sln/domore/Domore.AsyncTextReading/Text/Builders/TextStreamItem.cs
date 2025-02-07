namespace Domore.Text.Builders {
    public sealed class TextStreamItem {
        internal TextStreamItem(string text) {
            Text = text;
            Clear = Text == null;
        }

        public bool Clear { get; }
        public string Text { get; }
    }
}
