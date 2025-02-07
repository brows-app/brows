using System;

namespace Brows {
    internal sealed class FtpListing {
        public string Kind => nameof(FtpListing);

        public string Text { get; }
        public TimeSpan Time { get; }

        public FtpListing(TimeSpan time, string text) {
            Text = text;
            Time = time;
        }

        public sealed override string ToString() {
            return Text;
        }
    }
}
