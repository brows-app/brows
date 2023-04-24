using Brows.Gui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class EntryDataView : IEntryDataView {
        private List<string> KeyList =>
            _KeyList ?? (
            _KeyList = Config.Key.ToList());
        private List<string> _KeyList;

        private EntryDataViewConfig Config =>
            _Config ?? (
            _Config = new EntryDataViewConfig(Provider));
        private EntryDataViewConfig _Config;

        public IReadOnlyList<string> Keys =>
            KeyList;

        public IEntrySorting Sorting {
            get => _Sorting ?? (_Sorting = EntrySorting.From(Config.Sort));
            private set => _Sorting = value;
        }
        private IEntrySorting _Sorting;

        public IEntryObservationController Controller {
            get => _Controller;
            set {
                if (_Controller != value) {
                    _Controller = value;
                    Refresh();
                }
            }
        }
        private IEntryObservationController _Controller;

        public EntryProvider Provider { get; }
        public EntryObservationSource Source { get; }

        public EntryDataView(EntryProvider provider, EntryObservationSource source) {
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public async Task Init(CancellationToken token) {
            await Config.Init(token);
        }

        public string[] Sort(IEntrySorting sorting) {
            if (sorting == null) {
                var c = Controller;
                if (c != null) {
                    c.Sort(null);
                }
                Sorting = null;
                Config.Set(sort: null);
                return null;
            }
            var kSort = new Dictionary<string, EntrySortDirection?>();
            var names = sorting.Keys.ToArray();
            for (var i = 0; i < names.Length; i++) {
                var n = names[i];
                var k = Provider.Data.Definition.Key.Lookup(n);
                if (k != null) {
                    kSort[k] = sorting[n];
                }
                names[i] = k;
            }
            if (kSort.Count > 0) {
                var entries = Source.Observation;
                var ready = kSort
                    .Where(s => s.Value.HasValue)
                    .SelectMany(s => entries.Select(e => e[s.Key].Ready));
                async void begin() {
                    await Task.WhenAll(ready);
                }
                begin();
            }
            Sorting = EntrySorting.From(kSort);
            Config.Set(kSort);
            Controller?.Sort(Sorting);
            return names;
        }

        public bool Has(string name) {
            var key = Provider.Data.Definition.Key.Lookup(name);
            if (key != null) {
                return KeyList.Contains(key);
            }
            return false;
        }

        public string[] Add(params string[] names) {
            if (null == names) throw new ArgumentNullException(nameof(names));
            for (var i = 0; i < names.Length; i++) {
                var n = names[i];
                var k = Provider.Data.Definition.Key.Lookup(n);
                if (k != null) {
                    var c = Controller;
                    if (c != null) {
                        c.AddData(Provider.Data.Definition.Get(k));
                    }
                    KeyList.Add(k);
                }
                names[i] = k;
            }
            if (names.Any(n => n != null)) {
                Config.Set(KeyList);
            }
            return names;
        }

        public string[] Remove(params string[] names) {
            if (null == names) throw new ArgumentNullException(nameof(names));
            for (var i = 0; i < names.Length; i++) {
                var n = names[i];
                var k = Provider.Data.Definition.Key.Lookup(n);
                if (k != null) {
                    var c = Controller;
                    if (c != null) {
                        c.RemoveData(k);
                    }
                    KeyList.Remove(k);
                }
                names[i] = k;
            }
            if (names.Any(n => n != null)) {
                Config.Set(KeyList);
            }
            return names;
        }

        public void Refresh() {
            if (KeyList.Count == 0) {
                KeyList.AddRange(Provider.Config.DefaultKeys);
            }
            var controller = Controller;
            if (controller != null) {
                controller.ClearData();
                foreach (var key in KeyList) {
                    controller.AddData(Provider.Data.Definition.Get(key));
                }
            }
            Sort(Sorting);
        }

        public void Clear() {
            var c = Controller;
            if (c != null) {
                c.ClearData();
            }
            KeyList.Clear();
            Config.Set(key: null);
        }

        public void Reset() {
            Clear();
            Sort(null);
            Sorting = EntrySorting.From(Provider.Config.DefaultSorting);
            Refresh();
        }
    }
}
