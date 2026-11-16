using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace Brows.Entries;

internal sealed class EntryListViewItemsListView : ListView {
    protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue) {
        ItemsSourceChanged?.Invoke(this, EventArgs.Empty);
        base.OnItemsSourceChanged(oldValue, newValue);
    }

    protected sealed override DependencyObject GetContainerForItemOverride() {
        return new EntryListViewItemContainer {
            ParentListView = this
        };
    }

    internal event EventHandler ItemsSourceChanged;
}
