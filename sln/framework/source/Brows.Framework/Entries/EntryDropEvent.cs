using Brows.Panels;

namespace Brows.Entries;

/// <summary>
/// Handles files dropped on an entry.
/// </summary>
/// <param name="sender">
/// The object that raised the event.
/// </param>
/// <param name="e">
/// The event data.
/// </param>
public delegate void EntryDropEventHandler(object sender, EntryDropEventArgs e);

/// <summary>
/// Describes files dropped on an entry.
/// </summary>
public sealed class EntryDropEventArgs : EventArgs {
    /// <summary>
    /// Gets the drop target and offered file paths.
    /// </summary>
    public IPanelDrop Drop { get; }

    /// <summary>
    /// Initializes an entry drop event.
    /// </summary>
    /// <param name="drop">
    /// The drop target and offered files.
    /// </param>
    public EntryDropEventArgs(IPanelDrop drop) {
        Drop = drop;
    }
}
