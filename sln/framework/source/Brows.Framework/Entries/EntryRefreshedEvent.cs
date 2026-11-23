namespace Brows.Entries;

/// <summary>
/// Handles completion of an entry refresh.
/// </summary>
/// <param name="sender">
/// The object that raised the event.
/// </param>
/// <param name="e">
/// The event data.
/// </param>
public delegate void EntryRefreshedEventHandler(object sender, EntryRefreshedEventArgs e);

/// <summary>
/// Reports completion of an entry refresh.
/// </summary>
public sealed class EntryRefreshedEventArgs : EventArgs {
}
