namespace Brows {
    internal class EntryThumbnailData : IEntryData {
        public string Key => nameof(Entry.Thumbnail);
        public object Value => null;
        public void Refresh() {
        }
    }
}
