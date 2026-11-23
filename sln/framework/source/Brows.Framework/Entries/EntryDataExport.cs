namespace Brows.Entries;

/// <summary>
/// Makes a typed entry data definition discoverable as a service.
/// </summary>
/// <typeparam name="TEntry">
/// The entry type whose data is read.
/// </typeparam>
/// <typeparam name="TValue">
/// The type of value read for each entry.
/// </typeparam>
public abstract class EntryDataExport<TEntry, TValue> : EntryDataDefinition<TEntry, TValue>,
                                                        IEntryDataExport
where TEntry : IEntry {
    /// <summary>
    /// Initializes a typed entry data definition.
    /// </summary>
    /// <param name="key">
    /// The data key, or null to use the concrete type name.
    /// </param>
    protected EntryDataExport(string key = null) : base(key) {
    }
}
