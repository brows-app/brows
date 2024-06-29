using Brows.Config;
using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class EntryDataViewConfig {
        private EntryDataViewConfigData Data {
            get => _Data ??= new();
            set => _Data = value;
        }
        private EntryDataViewConfigData _Data;

        private ConfigClass Config => _Config ??= new(Provider.GetType());
        private ConfigClass _Config;

        private bool Default(IEnumerable<string> keys) {
            if (null == keys) throw new ArgumentNullException(nameof(keys));
            var defaultKeys = keys.ToList();
            if (defaultKeys.Count != Key.Count) {
                return false;
            }
            for (var i = 0; i < defaultKeys.Count; i++) {
                if (defaultKeys[i] != Key[i]) {
                    return false;
                }
            }
            return true;
        }

        private bool Default(IEnumerable<KeyValuePair<string, EntrySortDirection>> sort) {
            if (null == sort) throw new ArgumentNullException(nameof(sort));
            var defaultSort = sort.ToDictionary(pair => pair.Key, pair => pair.Value);
            if (defaultSort.Count != Sort.Count) {
                return false;
            }
            foreach (var k in defaultSort.Keys) {
                if (Sort.TryGetValue(k, out var value) == false) {
                    return false;
                }
                if (defaultSort[k] != value) {
                    return false;
                }
            }
            return true;
        }

        private void Set() {
            var data =
                (Data.Keys.Count == 0 || Default(Provider.Config.DefaultKeys)) &&
                (Data.Sort.Count == 0 || Default(Provider.Config.DefaultSorting))
                    ? null
                    : Data;
            Config.Set(Provider.ID, data);
        }

        public IReadOnlyList<string> Key => Data.Keys;
        public IReadOnlyDictionary<string, EntrySortDirection> Sort => Data.Sort;

        public Provider Provider { get; }

        public EntryDataViewConfig(Provider provider) {
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public async Task Init(CancellationToken token) {
            Data = await Config.Get(Provider.ID, token) ?? new EntryDataViewConfigData {
                Keys = Provider.Config.DefaultKeys.ToList(),
                Sort = Provider.Config.DefaultSorting.ToDictionary(pair => pair.Key, pair => pair.Value)
            };
        }

        public void Set(IEnumerable<string> key) {
            Data.Keys = key?.ToList();
            Set();
        }

        public void Set(IEnumerable<KeyValuePair<string, EntrySortDirection?>> sort) {
            Data.Sort = sort?
                .Where(pair => pair.Value.HasValue)?
                .GroupBy(pair => pair.Key)?
                .ToDictionary(pair => pair.Key, pair => pair.Last().Value.Value);
            Set();
        }

        private sealed class EntryDataViewConfigData {
            public IReadOnlyList<string> Keys {
                get => _Key ?? (_Key = new List<string>());
                set => _Key = value;
            }
            private IReadOnlyList<string> _Key;

            public IReadOnlyDictionary<string, EntrySortDirection> Sort {
                get => _Sort ?? (_Sort = new Dictionary<string, EntrySortDirection>());
                set => _Sort = value;
            }
            private IReadOnlyDictionary<string, EntrySortDirection> _Sort;
        }

        private sealed class ConfigClass {
            private IConfig<EntryDataViewConfigSet> Cache => _Cache ??=
                Configure.Data<EntryDataViewConfigSet>(ProviderType.Name);
            private IConfig<EntryDataViewConfigSet> _Cache;

            public Type ProviderType { get; }

            public ConfigClass(Type providerType) {
                ProviderType = providerType ?? throw new ArgumentNullException(nameof(providerType));
            }

            public async Task<EntryDataViewConfigData> Get(string id, CancellationToken token) {
                var data = await Cache.Load(token);
                var dict = data.Set;
                if (dict.TryGetValue(id, out var value)) {
                    return value;
                }
                return null;
            }

            public void Set(string id, EntryDataViewConfigData config) {
                var data = Cache.Loaded;
                var dict = data.Set;
                if (config == null) {
                    if (dict.Remove(id)) {
                        data.Changed();
                    }
                    return;
                }
                dict[id] = config;
                data.Changed();
            }

            private sealed class EntryDataViewConfigSet : Notifier {
                public Dictionary<string, EntryDataViewConfigData> Set {
                    get => _Set ?? (_Set = new Dictionary<string, EntryDataViewConfigData>());
                    set => _Set = value;
                }
                private Dictionary<string, EntryDataViewConfigData> _Set;

                public void Changed() {
                    NotifyPropertyChanged(nameof(Set));
                }
            }
        }
    }
}
