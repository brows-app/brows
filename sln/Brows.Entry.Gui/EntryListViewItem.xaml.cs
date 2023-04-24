using System.Windows.Controls;
using System.Windows.Input;

namespace Brows {
    partial class EntryListViewItem {
        private bool MouseDownDeferred;

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

        protected override void OnMouseDown(MouseButtonEventArgs e) {
            if (e != null) {
                switch (e.ChangedButton) {
                    case MouseButton.Left:
                    case MouseButton.Right:
                        if (MouseDownDeferred) {
                            MouseDownDeferred = false;
                        }
                        else {
                            var entry = DataContext as IEntry;
                            if (entry != null) {
                                var select = entry.Select;
                                if (select) {
                                    MouseDownDeferred = true;
                                    e.Handled = true;
                                }
                            }
                        }
                        break;
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseButtonEventArgs e) {
            if (e != null) {
                switch (e.ChangedButton) {
                    case MouseButton.Left:
                    case MouseButton.Right:
                        if (MouseDownDeferred) {
                            RaiseEvent(new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, e.ChangedButton) { RoutedEvent = MouseDownEvent });
                        }
                        break;
                }
            }
            base.OnMouseUp(e);
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
