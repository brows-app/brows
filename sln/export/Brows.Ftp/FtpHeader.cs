using System;
using System.Collections.Generic;

namespace Brows {
    internal sealed class FtpHeader {
        public string Kind => nameof(FtpHeader);

        private IReadOnlyList<string> Part => _Part ??= Text.Split([], 2);
        private IReadOnlyList<string> _Part;

        public int? Code => _Code ??=
            Part.Count > 0 && int.TryParse(Part[0], out var code)
                ? code
                : null;
        private int? _Code;

        public string Message => _Message ??= Part.Count > 1 && Code.HasValue
            ? Part[1]
            : Text;
        private string _Message;

        public string Text { get; }
        public TimeSpan Time { get; }

        public FtpHeader(TimeSpan time, string text) {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Time = time;
        }

        public sealed override string ToString() {
            return Text;
        }
    }
}
