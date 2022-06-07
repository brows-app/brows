using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;

namespace Brows.Gui {
    using Extensions;
    using Logger;

    internal class EntryCollectionController : CollectionController<IEntryCollectionController>, IEntryCollectionController {
        private static readonly ILog Log = Logging.For(typeof(EntryCollectionController));

        private Dictionary<string, List<EntryGridViewColumnProxy>> Columns = new Dictionary<string, List<EntryGridViewColumnProxy>>();
        private ListView ListView => UserControl.ListView;
        private GridView GridView => UserControl.ListView.GridView;

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            var selectedEntries = ListView.SelectedItems.OfType<IEntry>().ToList();
            var deselectedEntries = SelectedEntries.Except(selectedEntries);
            foreach (var entry in deselectedEntries) {
                entry.Selected = false;
            }
            foreach (var entry in selectedEntries) {
                entry.Selected = true;
            }
            SelectedEntries = selectedEntries;
            SelectionChanged?.Invoke(this, e);
        }

        private List<EntryGridViewColumnProxy> List(string key) {
            if (Columns.TryGetValue(key, out var list) == false) {
                Columns[key] = list = new List<EntryGridViewColumnProxy>();
            }
            return list;
        }

        public event EventHandler SelectionChanged;

        public new EntryCollectionControl UserControl { get; }

        public bool Focused => ListView.IsKeyboardFocusWithin;
        public IEntry CurrentEntry => CurrentItem as IEntry;
        public IReadOnlyList<IEntry> SelectedEntries { get; private set; } = new List<IEntry>();

        public EntryCollectionController(EntryCollectionControl userControl) : base(userControl, userControl.ListView) {
            UserControl = userControl ?? throw new ArgumentNullException(nameof(userControl));
            UserControl.ListView.SelectionChanged += ListView_SelectionChanged;
        }

        public bool MoveCurrentTo(IEntry item) {
            ListView.ScrollIntoView(item);

            var moved = Items.MoveCurrentTo(item);
            if (moved == false) return false;

            var control = ItemContainerGenerator.ContainerFromItem(item) as ListViewItem;
            if (control == null) return false;

            return control.Focus();
        }

        public bool Focus() {
            if (Log.Info()) {
                Log.Info(nameof(Focus));
            }
            var currentPosition = CurrentPosition;
            if (currentPosition < 0) {
                if (Items.Count > 0) {
                    Items.MoveCurrentToFirst();
                    if (Log.Info()) {
                        Log.Info(nameof(Items.MoveCurrentToFirst));
                    }
                }
            }
            var currentItem = CurrentItem;
            if (currentItem != null) {
                var listViewItem = ItemContainerGenerator.ContainerFromItem(currentItem) as ListViewItem;
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

        public void Removed(IEntry entry) {
            if (Focused) {
                Focus();
            }
        }

        public void Sort(IReadOnlyDictionary<string, EntrySortDirection?> sorting) {
            var listViewItems = ListView.Items;
            var liveSorting = listViewItems.LiveSortingProperties;
            var sortDescriptions = listViewItems.SortDescriptions;
            using (listViewItems.DeferRefresh()) {
                listViewItems.IsLiveSorting = true;
                liveSorting.Clear();
                sortDescriptions.Clear();
                if (sorting == null) {
                    foreach (var list in Columns.Values) {
                        foreach (var column in list) {
                            column.Sorting(null);
                        }
                    }
                }
                else {
                    foreach (var pair in sorting) {
                        var key = pair.Key;
                        var dir = pair.Value;
                        var list = List(key);
                        var path = default(string);
                        foreach (var column in list) {
                            column.Sorting(dir);
                            path = column.BindingValuePath;
                        }
                        if (path != null) {
                            if (dir.HasValue) {
                                liveSorting.Add(path);
                                sortDescriptions.Add(new SortDescription(
                                    propertyName: path,
                                    direction: dir.Value.ToListSortDirection()));
                            }
                        }
                    }
                }
            }
        }

        public void AddColumn(string key, IEntryColumn info) {
            var
            col = new EntryGridViewColumnProxy(key, info);
            col.AddTo(GridView);
            var
            list = List(key);
            list.Add(col);
        }

        public bool RemoveColumn(string key) {
            var list = List(key);
            var removed = false;
            foreach (var column in list) {
                var
                r = column.RemoveFrom(GridView);
                removed |= r;
            }
            return removed;
        }

        public bool HasColumn(string key) {
            var list = List(key);
            return list.Count > 0;
        }

        public void ClearColumns() {
            GridView.Columns.Clear();
        }

        IEnumerable<string> IEntryCollectionController.Columns =>
            Columns
                .Where(c => c.Value.Count > 0)
                .Select(c => c.Key);
    }
}
