namespace Brows.Commands;

/// <summary>
/// Holds the parts of a panel that the preview focus command can move the focus to.
/// </summary>
internal enum PreviewFocusTarget {
    /// <summary>
    /// The preview of the current message.
    /// </summary>
    Preview = 0,

    /// <summary>
    /// The list of the messages.
    /// </summary>
    Entries = 1,
}
