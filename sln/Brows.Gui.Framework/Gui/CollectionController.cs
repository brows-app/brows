using Domore.Logs;
using System;
using System.Windows.Controls;
using System.Windows.Data;

namespace Brows.Gui {
    public class CollectionController<TController> : Controller<TController> where TController : class {
        private static readonly ILog Log = Logging.For(typeof(CollectionController<TController>));

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
            CurrentChanged?.Invoke(this, e);
        }

        protected object CurrentItem => Items.CurrentItem;
        protected ItemCollection Items => ItemsControl.Items;
        protected ItemContainerGenerator ItemContainerGenerator => ItemsControl.ItemContainerGenerator;

        protected ScrollViewer ScrollViewer =>
            _ScrollViewer ?? (
            _ScrollViewer = ItemsControl.FindVisualChild<ScrollViewer>());
        private ScrollViewer _ScrollViewer;

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
