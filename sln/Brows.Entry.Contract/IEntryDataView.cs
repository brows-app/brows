namespace Brows {
    public interface IEntryDataView {
        IEntrySorting Sorting { get; }
        bool Has(string name);
        void Clear();
        void Refresh();
        void Reset();
        string[] Sort(IEntrySorting sorting);
        string[] Add(params string[] names);
        string[] Remove(params string[] names);
    }
}
