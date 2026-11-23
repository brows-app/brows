namespace Brows.Entries;

/// <summary>
/// Handles a change in an entry's selection state.
/// </summary>
/// <param name="sender">
/// The object that raised the event.
/// </param>
/// <param name="e">
/// The event data.
/// </param>
public delegate void EntrySelectedEventHandler(object sender, EntrySelectedEventArgs e);

/// <summary>
/// Describes an entry's new selection state.
/// </summary>
public class EntrySelectedEventArgs : EventArgs {
    /// <summary>
    /// Gets whether the entry is selected.
    /// </summary>
    public bool Select { get; }

    /// <summary>
    /// Initializes an entry selection event.
    /// </summary>
    /// <param name="select">
    /// Whether the entry is now selected.
    /// </param>
    public EntrySelectedEventArgs(bool select) {
        Select = select;
    }
}
