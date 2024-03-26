using Brows.Exports;

namespace Brows.Commands {
    internal sealed class ClipboardData : IClipboardGetIOData, IClipboardSetIOData {
        public bool MoveOnPaste { get; set; }
    }
}
