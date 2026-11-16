using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;

namespace Brows.Composition;

internal sealed class ClipboardTestTransport : IClipboardTransport {
    public StringCollection Files { get; } = new();
    public Dictionary<string, object> OtherFormats { get; } = new();
    public int ClearCount { get; private set; }
    public int PublishFileDropListCount { get; private set; }
    public bool HasFileDropList { get; set; }

    public DragDropEffects PreferredDropEffect { get; set; } = DragDropEffects.Copy;

    public void Clear() {
        ClearCount++;
        Files.Clear();
        OtherFormats.Clear();
        HasFileDropList = false;
        PreferredDropEffect = DragDropEffects.Copy;
    }

    public bool ContainsFileDropList() {
        return HasFileDropList;
    }

    public StringCollection GetFileDropList() {
        var files = new StringCollection();
        var fileArray = new string[Files.Count];
        Files.CopyTo(fileArray, 0);
        files.AddRange(fileArray);
        return files;
    }

    public void PublishFileDropList(StringCollection files, DragDropEffects preferredDropEffect) {
        Files.Clear();
        var fileArray = new string[files.Count];
        files.CopyTo(fileArray, 0);
        Files.AddRange(fileArray);
        OtherFormats.Clear();
        HasFileDropList = true;
        PreferredDropEffect = preferredDropEffect;
        PublishFileDropListCount++;
    }
}
