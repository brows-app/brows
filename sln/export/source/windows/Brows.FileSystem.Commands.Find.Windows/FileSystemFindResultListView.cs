using Brows.Gui;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Brows;

internal sealed class FileSystemFindResultListView : ListView {
    static FileSystemFindResultListView() {
        /*
         * Register before ItemsControl's property-changed callback attaches the source and creates its CollectionView.
         */
        ItemsSourceProperty.OverrideMetadata(
            typeof(FileSystemFindResultListView),
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
