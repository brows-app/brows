using Brows.Panels;
using System.Threading;

namespace Brows.Gui;

/// <summary>
/// Controls the preview of the current message of a mailbox.
/// </summary>
internal interface IMBoxEntryPreviewController : IController {
    /// <summary>
    /// Gets whether the preview owns the keyboard focus.
    /// </summary>
    bool HasKeyboardFocus { get; }

    /// <summary>
    /// Moves the keyboard focus to the preview.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the preview took the focus.
    /// </returns>
    bool RequestFocus();

    /// <summary>
    /// Moves the keyboard focus to the preview, after the work of the user interface that is
    /// running now ends.
    /// </summary>
    /// <remarks>
    /// The panel takes the focus back when the palette closes, so a preview that is focused
    /// while the palette is still open loses the focus again at once.
    /// </remarks>
    void RequestFocusLater(IPanel panel, IPanelCollection panels, CancellationToken token);
}
