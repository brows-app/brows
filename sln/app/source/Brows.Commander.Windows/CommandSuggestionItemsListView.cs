using Brows.Gui;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Brows;

internal sealed class CommandSuggestionItemsListView : ListView {
    static CommandSuggestionItemsListView() {
        /*
         * Register before ItemsControl's property-changed callback attaches the source and creates its CollectionView.
         */
        ItemsSourceProperty.OverrideMetadata(
            typeof(CommandSuggestionItemsListView),
            new FrameworkPropertyMetadata((PropertyChangedCallback)null, CoerceItemsSource));
    }

    private static object CoerceItemsSource(DependencyObject element, object baseValue) {
        if (baseValue is ISyncedCollection collection) {
            BindingOperations.EnableCollectionSynchronization(
                collection: collection,
                context: null,
                synchronizationCallback: collection.Sync);
        }
        return baseValue;
    }
}
