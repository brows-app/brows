namespace Brows;

public abstract class FileProtocolStreamSource<TEntry> : EntryStreamSource<TEntry> where TEntry : FileProtocolEntry {
    internal TEntry FileProtocolEntry => Entry;

    protected FileProtocolStreamSource(TEntry entry) : base(entry) {
    }
}
