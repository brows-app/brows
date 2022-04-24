using System;
using System.Windows.Controls;
using System.Windows.Data;

namespace Brows.Gui {
    using Logger;

    public class CollectionController<TController> : Controller<TController>, ICollectionController where TController : class {
        private ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(CollectionController<TController>)));
        private ILog _Log;

        private ListCollectionView CollectionView =>
            _CollectionView ?? (
            _CollectionView = (ListCollectionView)CollectionViewSource.GetDefaultView(ItemsControl.ItemsSource));
        private ListCollectionView _CollectionView;

        private void Items_CurrentChanged(object sender, EventArgs e) {
            if (Log.Info()) {
                Log.Info(
                    nameof(Items_CurrentChanged),
                    nameof(Items.CurrentItem) + " > " + Items.CurrentItem,
                    nameof(Items.CurrentPosition) + " > " + Items.CurrentPosition);
            }
            OnCurrentChanged(e);
        }

        protected object CurrentItem => Items.CurrentItem;
        protected ItemCollection Items => ItemsControl.Items;
        protected ItemContainerGenerator ItemContainerGenerator => ItemsControl.ItemContainerGenerator;

        protected ScrollViewer ScrollViewer =>
            _ScrollViewer ?? (
            _ScrollViewer = ItemsControl.FindVisualChild<ScrollViewer>());
        private ScrollViewer _ScrollViewer;

        protected virtual void OnCurrentChanged(EventArgs e) {
            CurrentChanged?.Invoke(this, e);
        }

        public event EventHandler CurrentChanged;

        public ItemsControl ItemsControl { get; }
        public int CurrentPosition => Items.CurrentPosition;

        public CollectionController(UserControl userControl, ItemsControl itemsControl) : base(userControl) {
            ItemsControl = itemsControl ?? throw new ArgumentNullException(nameof(itemsControl));
            ItemsControl.Items.CurrentChanged += Items_CurrentChanged;
        }
    }

    public class CollectionController<TController, TItem> : CollectionController<TController> where TController : class where TItem : class {
        public new TItem CurrentItem {
            get {
                return ItemsControl.Items.CurrentItem as TItem;
            }
        }

        public CollectionController(UserControl userControl, ItemsControl itemsControl) : base(userControl, itemsControl) {
        }
    }
}
