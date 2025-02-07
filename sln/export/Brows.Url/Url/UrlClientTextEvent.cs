using System;

namespace Brows.Url {
    public delegate void UrlClientTextEventHandler(object sender, UrlClientTextEventArgs e);

    public sealed class UrlClientTextEventArgs : EventArgs {
        public bool Clear { get; }
        public bool Complete { get; }
        public string Text { get; }
        public DateTime Time { get; }
        public UrlClientTextKind Kind { get; }

        public UrlClientTextEventArgs(DateTime time, UrlClientTextKind kind, string text = null, bool clear = false, bool complete = false) {
            Kind = kind;
            Time = time;
            Text = text;
            Clear = clear;
            Complete = complete;
        }
    }
}
