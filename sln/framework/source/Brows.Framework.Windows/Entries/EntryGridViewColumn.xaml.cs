namespace Brows.Entries;

partial class EntryGridViewColumn {
    public string DataKey { get; }

    public EntryGridViewColumn(string dataKey) {
        DataKey = dataKey;
        InitializeComponent();
    }
}
