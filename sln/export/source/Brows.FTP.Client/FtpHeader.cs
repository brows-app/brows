using System.Collections.Generic;

namespace Brows;

internal sealed class FtpHeader {
    public string Kind => nameof(FtpHeader);

    private IReadOnlyList<string> Part => field ??= Text.Split([], 2);

    public int? Code => field ??=
        Part.Count > 0 && int.TryParse(Part[0], out var code)
            ? code
            : null;

    public string Message => field ??= Part.Count > 1 && Code.HasValue
        ? Part[1]
        : Text;

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
