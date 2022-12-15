using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Collections.ObjectModel;
    using Gui;
    using Threading.Tasks;

    internal class EntryCollection : CollectionSource<IEntry>, IEntryCollection, IControlled<IEntryCollectionController> {
        private readonly List<string> Columns = new List<string>();

        private TaskHandler TaskHandler =>
            _TaskHandler ?? (
            _TaskHandler = new TaskHandler<EntryCollection>());
        private TaskHandler _TaskHandler;

        private IEntryColumn Column(string key) {
            if (ColumnInfo.TryGetValue(key, out var value)) {
                return value;
            }
            return null;
        }

        private async Task Sort(IReadOnlyDictionary<string, EntrySortDirection?> sorting) {
            if (sorting == null) {
                Sorting = null;
                Controller?.Sort(null);
            }
            else {
                if (sorting.Count > 0) {
                    var entries = List;
                    var ready = sorting
                        .Where(s => s.Value.HasValue)
                        .SelectMany(s => entries.Select(e => e[s.Key].Ready));
                    await Task.WhenAll(ready);
                }
                Sorting = sorting;
                Controller?.Sort(Sorting);
            }
        }

        private void Controller_CurrentChanged(object sender, EventArgs e) {
            CurrentChanged?.Invoke(this, e);
        }

        private void Controller_SelectionChanged(object sender, EventArgs e) {
            SelectionChanged?.Invoke(this, e);
        }

        public event EventHandler CurrentChanged;
        public event EventHandler SelectionChanged;

        public IEnumerable<string> ColumnDefaults {
            get => _ColumnDefaults ?? (_ColumnDefaults = Array.Empty<string>());
            set => Change(ref _ColumnDefaults, value, nameof(ColumnDefaults));
        }
        private IEnumerable<string> _ColumnDefaults;

        public IReadOnlyDictionary<string, IEntryColumn> ColumnInfo {
            get => _ColumnInfo ?? (_ColumnInfo = new Dictionary<string, IEntryColumn>());
            set => Change(ref _ColumnInfo, value, nameof(ColumnInfo));
        }
        private IReadOnlyDictionary<string, IEntryColumn> _ColumnInfo;

        public Func<string, string> ColumnLookup {
            get => _ColumnLookup ?? (_ColumnLookup = s => s);
            set => Change(ref _ColumnLookup, value, nameof(ColumnLookup));
        }
        private Func<string, string> _ColumnLookup;

        public IEntry CurrentItem => Controller?.CurrentEntry;
        public int? CurrentPosition => Controller?.CurrentPosition;
        public IReadOnlyList<IEntry> Selection => Controller?.SelectedEntries;

        public IEntryCollectionController Controller {
            get => _Controller;
            set {
                var newValue = value;
                var oldValue = _Controller;
                if (oldValue != newValue) {
                    if (oldValue != null) {
                        oldValue.CurrentChanged -= Controller_CurrentChanged;
                        oldValue.SelectionChanged -= Controller_SelectionChanged;
                    }
                    if (newValue != null) {
                        newValue.CurrentChanged += Controller_CurrentChanged;
                        newValue.SelectionChanged += Controller_SelectionChanged;
                    }
                    _Controller = newValue;
                    RefreshColumns();
                }
            }
        }

        private IEntryCollectionController _Controller;

        public IReadOnlyDictionary<string, EntrySortDirection?> Sorting {
            get => _Sorting ?? (_Sorting = new Dictionary<string, EntrySortDirection?>());
            private set => Change(ref _Sorting, value, nameof(Sorting));
        }
        private IReadOnlyDictionary<string, EntrySortDirection?> _Sorting;

        public object SortCommand => Request.Create(
            execute: arg => {
                var key = arg?.ToString();
                if (key != null) {
                    var exist = Sorting.TryGetValue(key, out var value)
                        ? value
                        : default(EntrySortDirection?);
                    var cycle =
                        exist == null ? EntrySortDirection.Ascending :
                        exist == EntrySortDirection.Ascending ? EntrySortDirection.Descending :
                        default(EntrySortDirection?);
                    var
                    dict = Sorting.ToDictionary(pair => pair.Key, pair => pair.Value);
                    dict[key] = cycle;
                    TaskHandler.Begin(() => Sort(dict));
                }
            },
            canExecute: _ => true);

        public async Task<bool> Add(IEntry item, CancellationToken cancellationToken) {
            if (item != null) {
                var sorting = Sorting;
                if (sorting.Count > 0) {
                    var ready = sorting
                        .Where(s => s.Value.HasValue)
                        .Select(s => item[s.Key].Ready);
                    await Task.WhenAll(ready);
                }
                List.Add(item);
                return true;
            }
            return false;
        }

        public void Clear() {
            List.Clear();
        }

        public bool Remove(IEntry item) {
            var removed = List.Remove(item);
            if (removed) {
                Controller?.Removed(item);
            }
            return removed;
        }

        public bool HasColumn(string name) {
            var key = ColumnLookup(name);
            if (key != null) {
                return Columns.Contains(key);
            }
            return false;
        }

        public string[] AddColumns(params string[] names) {
            if (null == names) throw new ArgumentNullException(nameof(names));
            for (var i = 0; i < names.Length; i++) {
                var n = names[i];
                var k = ColumnLookup(n);
                if (k != null) {
                    Columns.Add(k);
                    Controller?.AddColumn(k, Column(k));
                }
                names[i] = k;
            }
            return names;
        }

        public string[] RemoveColumns(params string[] names) {
            if (null == names) throw new ArgumentNullException(nameof(names));
            for (var i = 0; i < names.Length; i++) {
                var n = names[i];
                var k = ColumnLookup(n);
                if (k != null) {
                    Columns.Remove(k);
                    Controller?.RemoveColumn(k);
                }
                names[i] = k;
            }
            return names;
        }

        public void ClearColumns() {
            Columns.Clear();
            Controller?.ClearColumns();
        }

        public void RefreshColumns() {
            var controller = Controller;
            if (controller != null) {
                controller.ClearColumns();

                var keys = Columns.Count == 0 ? ColumnDefaults : Columns;
                foreach (var key in keys) {
                    controller.AddColumn(key, Column(key));
                }
            }
        }

        public string[] SortColumns(IReadOnlyDictionary<string, EntrySortDirection?> sorting) {
            if (sorting == null) {
                Sorting = null;
                Controller?.Sort(null);
                return null;
            }
            var kDict = new Dictionary<string, EntrySortDirection?>();
            var names = sorting.Keys.ToArray();
            for (var i = 0; i < names.Length; i++) {
                var n = names[i];
                var k = ColumnLookup(n);
                if (k != null) {
                    kDict[k] = sorting[n];
                }
                names[i] = k;
            }
            TaskHandler.Begin(() => Sort(kDict));
            return names;
        }

        public bool? MoveCurrentItemTo(IEntry entry) {
            return Controller?.MoveCurrentTo(entry);
        }

        public bool? Focus() {
            return Controller?.Focus();
        }

        public void ClearSort() {
            Sorting = null;
            Controller?.Sort(null);
        }
    }
}
