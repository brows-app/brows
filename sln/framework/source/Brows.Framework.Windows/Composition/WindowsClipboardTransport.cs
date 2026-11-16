using System.Collections.Specialized;
using System.Windows;

namespace Brows.Composition;

internal sealed class WindowsClipboardTransport : IClipboardTransport {
    private ClipboardData Data => ClipboardData.Instance;

    public DragDropEffects PreferredDropEffect {
        get => Data.PreferredDropEffect;
        set => Data.PreferredDropEffect = value;
    }

    public void Clear() {
        Clipboard.Clear();
        Data.Reset();
    }

    public bool ContainsFileDropList() {
        return Clipboard.ContainsFileDropList();
    }

    public StringCollection GetFileDropList() {
        return Clipboard.GetFileDropList();
    }

    public void PublishFileDropList(StringCollection files, DragDropEffects preferredDropEffect) {
        Clipboard.SetFileDropList(files);
        Data.PreferredDropEffect = preferredDropEffect;
    }
}
