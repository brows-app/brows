using System.Windows.Controls;

namespace Brows.Entries;

internal sealed class EntryGridViewColumn : GridViewColumn {
    public string DataKey { get; }

    public EntryGridViewColumn(string dataKey) {
        DataKey = dataKey;
    }
}
