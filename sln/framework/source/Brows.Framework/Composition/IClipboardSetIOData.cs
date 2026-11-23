namespace Brows.Composition;

/// <summary>
/// Specifies how clipboard contents should be pasted.
/// </summary>
public interface IClipboardSetIOData {
    /// <summary>
    /// Gets whether pasting should move the source items.
    /// </summary>
    bool MoveOnPaste { get; }
}
