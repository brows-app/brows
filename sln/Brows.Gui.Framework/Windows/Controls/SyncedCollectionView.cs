using Brows.Gui;
using System.Collections;
using System.Windows.Controls;
using System.Windows.Data;

namespace Brows.Windows.Controls {
    public class SyncedCollectionView : ListView {
        protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue) {
            if (oldValue is ISyncedCollection) {
                BindingOperations.DisableCollectionSynchronization(oldValue);
            }
            if (newValue is ISyncedCollection collection) {
                BindingOperations.EnableCollectionSynchronization(newValue, null, collection.Sync);
            }
            base.OnItemsSourceChanged(oldValue, newValue);
        }

        ~SyncedCollectionView() {
            if (ItemsSource is ISyncedCollection collection) {
                BindingOperations.DisableCollectionSynchronization(collection);
            }
        }
    }
}
