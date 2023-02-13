using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Config;
    using Gui;
    using Threading.Tasks;

    internal class EntryCollection : Notifier, IEntryCollection, IControlled<IEntryCollectionController> {
        private readonly List<string> Columns = new();
        private readonly PanelData Data = new();

        private Type ProviderType;
        private bool ProviderLoad;
        private bool ProviderSave;
        private IEntrySorting ProviderSort;

        private TaskHandler TaskHandler =>
            _TaskHandler ?? (
            _TaskHandler = new TaskHandler<EntryCollection>());
        private TaskHandler _TaskHandler;

        private PanelConfig Config {
            get => _Config ?? (_Config = new PanelConfig());
            set => _Config = value;
        }
        private PanelConfig _Config;

        private ObservableCollection<IEntry> Source {
            get {
                if (_Source == null) {
                    _Source = new();
                    _Source.CollectionChanged += Source_CollectionChanged;
                    ((INotifyPropertyChanged)_Source).PropertyChanged += Source_PropertyChanged;
                }
                return _Source;
            }
            set {
                var newValue = value;
                var oldValue = _Source;
                if (oldValue != newValue) {
                    if (oldValue != null) {
                        oldValue.CollectionChanged -= Source_CollectionChanged;
                        ((INotifyPropertyChanged)oldValue).PropertyChanged -= Source_PropertyChanged;
                    }
                    if (newValue != null) {
                        newValue.CollectionChanged += Source_CollectionChanged;
                        ((INotifyPropertyChanged)newValue).PropertyChanged += Source_PropertyChanged;
                    }
                    _Source = newValue;
                    Controller?.Source(_Source);
                    NotifyPropertyChanged(nameof(Items));
                    NotifyPropertyChanged(nameof(Count));
                }
            }
        }
        private ObservableCollection<IEntry> _Source;

        private void Source_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            NotifyPropertyChanged(e);
        }

        private void Source_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
        }

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
                    var entries = Source;
                    var ready = sorting
                        .Where(s => s.Value.HasValue)
                        .SelectMany(s => entries.Select(e => e[s.Key].Ready));
                    await Task.WhenAll(ready);
                }
                Sorting = EntrySorting.From(sorting);
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

        public IEnumerable<string> ColumnDefault {
            get => _ColumnDefault ?? (_ColumnDefault = Array.Empty<string>());
            set => Change(ref _ColumnDefault, value, nameof(ColumnDefault));
        }
        private IEnumerable<string> _ColumnDefault;

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

        public int Count => Source.Count;
        public IEntry CurrentItem => Controller?.CurrentEntry;
        public int? CurrentPosition => Controller?.CurrentPosition;
        public IReadOnlyList<IEntry> Selection => Controller?.SelectedEntries ?? Array.Empty<IEntry>();
        public IReadOnlyList<IEntry> Items => Source;

        public IEntryCollectionController Controller {
            get => _Controller;
            set {
                var newValue = value;
                var oldValue = _Controller;
                if (oldValue != newValue) {
                    if (oldValue != null) {
                        oldValue.Source(Array.Empty<IEntry>());
                        oldValue.CurrentChanged -= Controller_CurrentChanged;
                        oldValue.SelectionChanged -= Controller_SelectionChanged;
                    }
                    if (newValue != null) {
                        newValue.Source(Source);
                        newValue.CurrentChanged += Controller_CurrentChanged;
                        newValue.SelectionChanged += Controller_SelectionChanged;
                    }
                    _Controller = newValue;
                    RefreshColumns();
                }
            }
        }

        private IEntryCollectionController _Controller;

        public IEntrySorting Sorting {
            get => _Sorting ?? (_Sorting = EntrySorting.Empty);
            private set => Change(ref _Sorting, value, nameof(Sorting));
        }
        private IEntrySorting _Sorting;

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

        public async Task<bool> Add(IReadOnlyCollection<IEntry> items, CancellationToken cancellationToken) {
            if (items == null) return false;
            if (items.Count == 0) return false;
            foreach (var item in items) {
                item.Begin(new EntryView(Columns));
            }
            var added = default(IEnumerable<Task>);
            var chunkSize = Config.AddChunkSize;
            var chunkDelay = Config.AddChunkDelay;
            var chunks = items.Chunk(chunkSize < 1 ? items.Count : chunkSize);
            foreach (var chunk in chunks) {
                var sorting = Sorting;
                if (sorting.Count > 0) {
                    var sort = sorting.Where(s => s.Value.HasValue).ToList();
                    if (sort.Count > 0) {
                        added = chunk.Select(async item => {
                            var ready = sort.Select(s => item[s.Key].Ready);
                            await Task.WhenAll(ready);
                            Source.Add(item);
                        });
                        await Task.WhenAll(added);
                    }
                }
                if (added == null) {
                    if (Source.Count == 0) {
                        Source = new ObservableCollection<IEntry>(chunk);
                    }
                    else {
                        foreach (var item in chunk) {
                            Source.Add(item);
                        }
                    }
                }
                if (chunkDelay > 0) {
                    await Task.Delay(chunkDelay, cancellationToken);
                }
            }
            return true;
        }

        public void Clear() {
            Source.Clear();
        }

        public async Task<bool> Remove(IReadOnlyCollection<IEntry> items, CancellationToken cancellationToken) {
            if (items == null) return false;
            if (items.Count == 0) return false;
            var controllerRemoved = false;
            var chunkSize = Config.AddChunkSize;
            var chunkDelay = Config.AddChunkDelay;
            var chunks = items.Chunk(chunkSize < 1 ? items.Count : chunkSize);
            foreach (var chunk in chunks) {
                foreach (var item in chunk) {
                    var sourceRemoved = Source.Remove(item);
                    if (sourceRemoved) {
                        controllerRemoved = true;
                    }
                }
                if (chunkDelay > 0) {
                    await Task.Delay(chunkDelay, cancellationToken);
                }
            }
            if (controllerRemoved) {
                Controller?.Removed();
            }
            return true;
        }

        public bool HasColumn(string name) {
            var key = ColumnLookup(name);
            if (key != null) {
                return Columns.Contains(key);
            }
            return false;
        }

        public IReadOnlyList<string> GetColumns() {
            return Columns;
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

                var columns = Columns;
                if (columns.Count == 0) {
                    columns.AddRange(ColumnDefault);
                }
                foreach (var key in columns) {
                    controller.AddColumn(key, Column(key));
                }
            }
        }

        public string[] SortColumns(IEntrySorting sorting) {
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

        public string[] SortColumns(IReadOnlyDictionary<string, EntrySortDirection?> sorting) {
            return SortColumns(EntrySorting.From(sorting));
        }

        public string[] SortColumns(IReadOnlyDictionary<string, EntrySortDirection> sorting) {
            return SortColumns(EntrySorting.From(sorting));
        }

        public void ResetColumns(IReadOnlyDictionary<string, EntrySortDirection?> sorting) {
            ClearColumns();
            ClearSort();
            RefreshColumns();
            SortColumns(sorting);
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

        public async Task Load(IEntryProvider provider, CancellationToken cancellationToken) {
            if (null == provider) throw new ArgumentNullException(nameof(provider));

            Config = await PanelConfig.Load(cancellationToken);
            ColumnInfo = provider.DataKeyColumns;
            ColumnLookup = provider.DataKeyLookup;
            ColumnDefault = provider.DataKeyDefault;

            var type = provider.GetType();
            if (type == ProviderType) {
                if (ProviderLoad == false && ProviderSave == false) {
                    ProviderSort = Sorting;
                }
            }
            var load = await Data.Load(provider, this, cancellationToken);
            if (load == false && type != ProviderType) {
                ResetColumns(provider.DataKeySorting);
                ProviderSort = Sorting;
            }
            if (load == false && type == ProviderType) {
                if (ProviderLoad || ProviderSave) {
                    ResetColumns(ProviderSort ?? provider.DataKeySorting);
                }
            }
            ProviderType = type;
            ProviderLoad = load;
            ProviderSave = false;
        }

        public async Task Save(IEntryProvider provider, CancellationToken cancellationToken) {
            ProviderSave = await Data.Save(provider, this, cancellationToken);
        }
    }
}
