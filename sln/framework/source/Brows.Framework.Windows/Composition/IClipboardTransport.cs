using System.Collections.Specialized;
using System.Windows;

namespace Brows.Composition;

internal interface IClipboardTransport {
    DragDropEffects PreferredDropEffect { get; set; }

    void Clear();
    bool ContainsFileDropList();
    StringCollection GetFileDropList();
    void PublishFileDropList(StringCollection files, DragDropEffects preferredDropEffect);
}
