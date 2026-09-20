namespace Brows.Entries;

public abstract class EntryDataExport<TEntry, TValue> : EntryDataDefinition<TEntry, TValue>,
                                                        IEntryDataExport
where TEntry : IEntry {
    protected EntryDataExport(string key = null) : base(key) {
    }
}
