namespace Brows.Composition;

/// <summary>
/// Receives information about how clipboard contents should be pasted.
/// </summary>
public interface IClipboardGetIOData {
    /// <summary>
    /// Sets whether pasting should move the source items.
    /// </summary>
    bool MoveOnPaste { set; }
}
