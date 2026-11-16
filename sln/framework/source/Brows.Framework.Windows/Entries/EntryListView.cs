using Brows.Windows.Controls;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Brows.Entries;

internal sealed class EntryListView : ModelView<IEntryObservation> {
    private EntryListViewItemsListView _ItemsListView;

    internal EntryListViewItemsListView ItemsListView => _ItemsListView;

    internal GridView GridView => _ItemsListView?.View as GridView;

    internal event EventHandler ItemsListViewChanged;

    public override void OnApplyTemplate() {
        var oldItemsListView = _ItemsListView;
        base.OnApplyTemplate();
        var newItemsListView = GetTemplateChild("PART_Items") as EntryListViewItemsListView;
        var itemsListViewChanged = !ReferenceEquals(oldItemsListView, newItemsListView);
        if (!itemsListViewChanged) {
            return;
        }
        var view = oldItemsListView?.View as EntryGridView;
        if (view is not null) {
            oldItemsListView.View = null;
        }
        if (newItemsListView is not null) {
            view ??= newItemsListView.View as EntryGridView;
            if (view is null) {
                view = new EntryGridView {
                    ColumnHeaderContainerStyle = TryFindResource("Brows.Style.EntryGridViewColumnHeader") as Style
                };
            }
            newItemsListView.View = view;
        }
        _ItemsListView = newItemsListView;
        ItemsListViewChanged?.Invoke(this, EventArgs.Empty);
    }
}
