using Domore.Logs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Brows.Gui {
    internal class EntryObservationController : Controller<IEntryObservationController>, IEntryObservationController {
        private static readonly ILog Log = Logging.For(typeof(EntryObservationController));

        private IComparer CustomSort;

        private List<IEntry> SelectedEntries {
            get => _SelectedEntries ?? (_SelectedEntries = new());
            set => _SelectedEntries = value;
        }
        private List<IEntry> _SelectedEntries;

        private Dictionary<string, List<EntryGridViewColumnProxy>> GridColumns =>
            _GridColumns ?? (
            _GridColumns = new());
        private Dictionary<string, List<EntryGridViewColumnProxy>> _GridColumns;

        private ListView ListView => Element.ListView;
        private GridView GridView => Element.ListView.GridView;
        private ListCollectionView ListCollectionView =>
            (ListCollectionView)CollectionViewSource.GetDefaultView(ListView.ItemsSource);

        private List<EntryGridViewColumnProxy> GridColumnList(string key) {
            if (GridColumns.TryGetValue(key, out var list) == false) {
                GridColumns[key] = list = new List<EntryGridViewColumnProxy>();
            }
            return list;
        }

        private void ListView_ItemsSourceChanged(object sender, EventArgs e) {
            var view = ListCollectionView;
            if (view != null) {
                view.CustomSort = CustomSort;
            }
        }

        private void ListView_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
            ManualInteraction?.Invoke(this, e);
        }

        private void ListView_PreviewKeyDown(object sender, KeyEventArgs e) {
            ManualInteraction?.Invoke(this, e);
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            var newlySelected = ListView.SelectedItems.Cast<IEntry>().ToList();
            var previouslySelected = SelectedEntries;
            var deselected = previouslySelected.Except(newlySelected);
            foreach (var entry in deselected) {
                entry.Select = false;
            }
            foreach (var entry in newlySelected) {
                entry.Select = true;
            }
            SelectedEntries = newlySelected;
        }

        private void ListView_MouseMove(object sender, MouseEventArgs e) {
            if (e != null) {
                if (e.LeftButton == MouseButtonState.Pressed) {
                    var source = (e.OriginalSource as FrameworkElement)?.DataContext as IEntry;
                    if (source != null && source.Select) {
                        DraggingSelected?.Invoke(this, e);
                    }
                }
            }
        }

        private void ListView_MouseUp(object sender, MouseButtonEventArgs e) {
            if (e != null) {
                switch (e.ChangedButton) {
                    case MouseButton.Left:
                    case MouseButton.Right:
                        var source = (e.OriginalSource as FrameworkElement)?.DataContext;
                        var isEntry = source is IEntry || source is IEntryData;
                        var nothing = !isEntry;
                        if (nothing) {
                            NothingSelected?.Invoke(this, e);
                        }
                        break;
                }
            }
        }

        private void Items_CurrentChanged(object sender, EventArgs e) {
            CurrentEntryChanged?.Invoke(this, e);
        }

        public event EventHandler NothingSelected;
        public event EventHandler ManualInteraction;
        public event EventHandler CurrentEntryChanged;
        public event EventHandler DraggingSelected;

        public new EntryObservationControl Element { get; }

        public EntryObservationController(EntryObservationControl element) : base(element) {
            Element = element ?? throw new ArgumentNullException(nameof(element));
            Element.ListView.Items.CurrentChanged += Items_CurrentChanged;
            Element.ListView.ItemsSourceChanged += ListView_ItemsSourceChanged;
            Element.ListView.MouseMove += ListView_MouseMove;
            Element.ListView.MouseUp += ListView_MouseUp;
            Element.ListView.PreviewKeyDown += ListView_PreviewKeyDown;
            Element.ListView.PreviewMouseDown += ListView_PreviewMouseDown;
            Element.ListView.SelectionChanged += ListView_SelectionChanged;
        }

        object IEntryObservationController.DraggingSource =>
            ListView;

        bool IEntryObservationController.CurrentEntry(IEntry item) {
            if (item == null) {
                return false;
            }
            ListView.ScrollIntoView(item);
            var moved = ListView.Items.MoveCurrentTo(item);
            if (moved == false) {
                return false;
            }
            var control = ListView.ItemContainerGenerator.ContainerFromItem(item) as ListViewItem;
            if (control == null) {
                return false;
            }
            return control.Focus();
        }

        IEntry IEntryObservationController.CurrentEntry() {
            return ListView.Items.CurrentItem as IEntry;
        }

        bool IEntryObservationController.Focus() {
            if (Log.Info()) {
                Log.Info(nameof(IEntryObservationController.Focus));
            }
            var currentPosition = ListView.Items.CurrentPosition;
            if (currentPosition < 0) {
                if (ListView.Items.Count > 0) {
                    ListView.Items.MoveCurrentToFirst();
                    if (Log.Info()) {
                        Log.Info(nameof(ListView.Items.MoveCurrentToFirst));
                    }
                }
            }
            var currentItem = ListView.Items.CurrentItem;
            if (currentItem != null) {
                var listViewItem = ListView.ItemContainerGenerator.ContainerFromItem(currentItem) as ListViewItem;
                if (listViewItem != null) {
                    var focus = listViewItem.IsKeyboardFocused || listViewItem.Focus();
                    if (Log.Info()) {
                        Log.Info(
                            $"{nameof(listViewItem.IsKeyboardFocused)} > {listViewItem.IsKeyboardFocused}",
                            $"{nameof(focus)} > {focus}");
                    }
                    return focus;
                }
            }
            var entryListViewFocus = ListView.Focus();
            if (Log.Info()) {
                Log.Info($"{nameof(entryListViewFocus)} > {entryListViewFocus}");
            }
            return false;
        }

        void IEntryObservationController.Sort(IEntrySorting sorting) {
            foreach (var list in GridColumns.Values) {
                foreach (var column in list) {
                    if (sorting == null) {
                        column.Sorting(null);
                    }
                    else {
                        if (sorting.TryGetValue(column.Key, out var value)) {
                            column.Sorting(value);
                        }
                        else {
                            column.Sorting(null);
                        }
                    }
                }
            }
            CustomSort = sorting == null
                ? null
                : new EntryComparer(sorting);
            var view = ListCollectionView;
            if (view != null) {
                view.CustomSort = CustomSort;
            }
        }

        bool IEntryObservationController.AddData(IEntryDataDefinition definition) {
            if (definition == null) {
                return false;
            }
            var key = definition.Key;
            var
            col = new EntryGridViewColumnProxy(definition);
            col.AddTo(GridView);
            var
            list = GridColumnList(key);
            list.Add(col);
            var sort = ListCollectionView?.CustomSort as EntryComparer;
            if (sort != null) {
                var sorting = sort.Sorting;
                if (sorting != null) {
                    if (sorting.TryGetValue(key, out var value)) {
                        col.Sorting(value);
                    }
                }
            }
            return true;
        }

        bool IEntryObservationController.RemoveData(string key) {
            var list = GridColumnList(key);
            var removed = false;
            foreach (var column in list) {
                var
                r = column.RemoveFrom(GridView);
                removed |= r;
            }
            return removed;
        }

        bool IEntryObservationController.HasData(string key) {
            var list = GridColumnList(key);
            return list.Count > 0;
        }

        void IEntryObservationController.ClearData() {
            GridView.Columns.Clear();
        }

        bool IEntryObservationController.Focused() {
            return ListView.IsKeyboardFocusWithin;
        }

        IDisposable IEntryObservationController.Updating() {
            return null; // ListCollectionView?.DeferRefresh();
        }
    }
}
