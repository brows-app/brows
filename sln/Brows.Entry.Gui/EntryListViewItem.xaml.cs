using System.Windows.Controls;
using System.Windows.Input;

namespace Brows {
    partial class EntryListViewItem {
        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e) {
            if (e != null) {
                var entry = DataContext as IEntry;
                if (entry != null) {
                    var relative = e.GetPosition(this);
                    var absolute = PointToScreen(relative);

                    //entry.Menu(new EntryContext { Panel = Panel, X = absolute.X, Y = absolute.Y });
                }
            }
            base.OnMouseRightButtonUp(e);
        }

        public EntryListView ParentListView {
            get => _ParentListView ?? (_ParentListView = ItemsControl.ItemsControlFromItemContainer(this) as EntryListView);
            set => _ParentListView = value;
        }
        private EntryListView _ParentListView;

        public EntryListViewItem() {
            InitializeComponent();
        }
    }
}
