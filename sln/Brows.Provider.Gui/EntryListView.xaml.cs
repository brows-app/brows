using System;
using System.Collections;
using System.Windows;

namespace Brows {
    sealed partial class EntryListView {
        protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue) {
            ItemsSourceChanged?.Invoke(this, EventArgs.Empty);
            base.OnItemsSourceChanged(oldValue, newValue);
        }

        protected sealed override DependencyObject GetContainerForItemOverride() {
            return new EntryListViewItem {
                ParentListView = this
            };
        }

        public event EventHandler ItemsSourceChanged;

        public EntryListView() {
            InitializeComponent();
        }
    }
}
