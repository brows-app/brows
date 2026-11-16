using Brows.Commands;
using Brows.Gui;
using Brows.Triggers;
using Brows.Windows.Controls;
using System;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;

namespace Brows;

internal sealed class CommandSuggestionCollectionControl :
    ModelView<CommandSuggestionCollection, ICommandSuggestionCollectionController>,
    ICommandSuggestionCollectionController {
    private ListView _ItemsListView;

    private event EventHandler _CurrentSuggestionChanged;

    private ListView GetItemsListView() {
        return GetTemplateChild("PART_Items") as ListView;
    }

    private void Items_CurrentChanged(object sender, EventArgs e) {
        _CurrentSuggestionChanged?.Invoke(this, e);
    }

    public override void OnApplyTemplate() {
        var oldItemsListView = _ItemsListView;
        base.OnApplyTemplate();
        var newItemsListView = GetItemsListView();
        var itemsListViewChanged = !ReferenceEquals(oldItemsListView, newItemsListView);
        if (!itemsListViewChanged) {
            return;
        }
        if (oldItemsListView is not null) {
            oldItemsListView.Items.CurrentChanged -= Items_CurrentChanged;
        }
        _ItemsListView = newItemsListView;
        if (_ItemsListView is null) {
            return;
        }

        var items = _ItemsListView.Items;
        items.CurrentChanged += Items_CurrentChanged;
        items.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ICommandSuggestion.Group)));
        items.IsLiveSorting = true;
        items.LiveSortingProperties.Add(nameof(ICommandSuggestion.GroupOrder));
        items.LiveSortingProperties.Add(nameof(ICommandSuggestion.Relevance));
        items.LiveSortingProperties.Add(nameof(ICommandSuggestion.Input));
        items.SortDescriptions.Add(new SortDescription(
            nameof(ICommandSuggestion.GroupOrder),
            ListSortDirection.Ascending));
        items.SortDescriptions.Add(new SortDescription(
            nameof(ICommandSuggestion.Relevance),
            ListSortDirection.Ascending));
        items.SortDescriptions.Add(new SortDescription(nameof(ICommandSuggestion.Input), ListSortDirection.Ascending));
    }

    event EventHandler ICommandSuggestionCollectionController.CurrentSuggestionChanged {
        add {
            _CurrentSuggestionChanged += value;
        }
        remove {
            _CurrentSuggestionChanged -= value;
        }
    }

    ICommandSuggestion ICommandSuggestionCollectionController.CurrentSuggestion =>
        _ItemsListView?.Items.CurrentItem as ICommandSuggestion;

    void ICommandSuggestionCollectionController.MoveCurrentSuggestion(PressKey pressKey) {
        _ItemsListView?.Move(pressKey);
    }
}
