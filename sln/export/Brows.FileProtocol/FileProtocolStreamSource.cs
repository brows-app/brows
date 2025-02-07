namespace Brows {
    public abstract class FileProtocolStreamSource<TEntry> : EntryStreamSource<TEntry> where TEntry : FileProtocolEntry {
        protected FileProtocolStreamSource(TEntry entry) : base(entry) {
        }
    }
}
