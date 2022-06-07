using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;

namespace Brows {
    partial class EntryListView {
        protected override void OnMouseUp(MouseButtonEventArgs e) {
            if (e != null) {
                switch (e.ChangedButton) {
                    case MouseButton.Left:
                    case MouseButton.Right:
                        var source = (e.OriginalSource as FrameworkElement)?.DataContext;
                        var isEntry = source is IEntry || source is IEntryData;
                        var notEntry = !isEntry;
                        if (notEntry) {
                            foreach (var item in Items) {
                                var entry = item as IEntry;
                                if (entry != null) {
                                    entry.Selected = false;
                                }
                            }
                        }
                        break;
                }
            }
            base.OnMouseUp(e);
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            if (e != null) {
                if (e.LeftButton == MouseButtonState.Pressed) {
                    var source = (e.OriginalSource as FrameworkElement)?.DataContext as IEntry;
                    if (source != null && source.Selected) {
                        var fileDropList = new StringCollection();
                        foreach (var item in Items) {
                            var entry = item as IEntry;
                            if (entry != null && entry.Selected) {
                                var file = entry.File;
                                if (file != null) {
                                    fileDropList.Add(file);
                                }
                            }
                        }
                        if (fileDropList.Count > 0) {
                            var data = new DataObject();
                            data.SetFileDropList(fileDropList);
                            DragDrop.DoDragDrop(this, data, DragDropEffects.Copy | DragDropEffects.Move);
                        }
                    }
                }
            }
            base.OnMouseMove(e);
        }

        protected override DependencyObject GetContainerForItemOverride() {
            return new EntryListViewItem {
                ParentListView = this
            };
        }

        public EntryListView() {
            InitializeComponent();
        }
    }
}
