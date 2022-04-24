namespace Brows {
    public interface IEntryData {
        string Key { get; }
        object Value { get; }
        void Refresh();
    }
}
