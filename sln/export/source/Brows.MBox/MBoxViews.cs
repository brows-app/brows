using System;

namespace Brows;

/// <summary>
/// Holds the views of a mailbox that the commands act on.
/// </summary>
[Flags]
internal enum MBoxViews {
    /// <summary>
    /// No view.
    /// </summary>
    None = 0,

    /// <summary>
    /// The preview of the current message.
    /// </summary>
    Preview = 2,
}
