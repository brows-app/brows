using System.Windows.Controls;
using System.Windows.Input;

namespace Brows {
    partial class EntryListViewItem {
        private bool MouseLeftButtonDownDeferred;

        private bool DeferMouseLeftButtonDown() {
            var modifiers = Keyboard.Modifiers;
            if (modifiers.HasFlag(ModifierKeys.Shift)) return false;
            if (modifiers.HasFlag(ModifierKeys.Control)) return true;
            if (IsSelected) {
                var parent = ParentListView;
                if (parent != null) {
                    if (parent.SelectedItems.Count > 1) {
                        return true;
                    }
                }
            }
            return false;
        }

        protected override void OnMouseEnter(MouseEventArgs e) {
            MouseLeftButtonDownDeferred = false;
            var entry = DataContext as IEntry;
            if (entry != null) {
                entry.Hovering = true;
            }
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(MouseEventArgs e) {
            MouseLeftButtonDownDeferred = false;
            var entry = DataContext as IEntry;
            if (entry != null) {
                entry.Hovering = false;
            }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) {
            if (e != null) {
                if (e.Handled == false) {
                    e.Handled = MouseLeftButtonDownDeferred = DeferMouseLeftButtonDown();
                }
            }
            base.OnMouseLeftButtonDown(e);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) {
            if (e != null) {
                if (MouseLeftButtonDownDeferred) {
                    MouseLeftButtonDownDeferred = false;

                    base.OnMouseLeftButtonDown(e);
                    e.Handled = true;
                }
            }
            base.OnMouseLeftButtonUp(e);
        }

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
