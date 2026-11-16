namespace Brows.MBox.Format;

/// <summary>
/// Specifies the mailbox storage dialect used when reading a mailbox.
/// </summary>
public enum MBoxFormat {
    /// <summary>
    /// The mboxrd dialect, where every body line matching <c>^&gt;+From </c> has exactly one
    /// <c>&gt;</c> removed before it is used.
    /// </summary>
    Mboxrd,

    /// <summary>
    /// The mboxo dialect, where only a body line matching exactly <c>&gt;From </c> has one
    /// <c>&gt;</c> removed before it is used.
    /// </summary>
    Mboxo,
}
