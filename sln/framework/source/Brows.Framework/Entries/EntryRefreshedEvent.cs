namespace Brows.Entries;

public delegate void EntryRefreshedEventHandler(object sender, EntryRefreshedEventArgs e);

public sealed class EntryRefreshedEventArgs : EventArgs {
}
