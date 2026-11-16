using Brows.Conf;
using Brows.Providers;
using Domore.Notification;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Entries;

internal sealed class EntryDataViewConfig {
    private EntryDataViewConfigData Data {
        get => field ??= new();
        set;
    }

    private IConfigurationData ConfigData => field ??=
        Provider.Import?.Find<IConfigurationData>() ?? new ConfigurationData();

    private ConfigClass Config => field ??= new(Provider.GetType(), ConfigData);

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

    internal static string DataName(Type providerType) {
        ArgumentNullException.ThrowIfNull(providerType);
        const string suffix = "Provider";
        var name = providerType.Name;
        var nameHasSuffix = name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        if (nameHasSuffix) {
            name = name.Substring(0, name.Length - suffix.Length);
        }
        if (name.Length == 0) {
            throw new ArgumentException(
                $"The provider type name '{providerType.Name}' is invalid.",
                nameof(providerType));
        }
        return $"entrydataview.{name.ToLowerInvariant()}";
    }

    public IReadOnlyList<string> Key => Data.Keys;
    public IReadOnlyDictionary<string, EntrySortDirection> Sort => Data.Sort;

    public EntryProvider Provider { get; }

    public EntryDataViewConfig(EntryProvider provider) {
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
            get => field ??= new List<string>();
            set;
        }

        public IReadOnlyDictionary<string, EntrySortDirection> Sort {
            get => field ??= new Dictionary<string, EntrySortDirection>();
            set;
        }
    }

    private sealed class ConfigClass {
        private IConfiguration<EntryDataViewConfigSet> Cache => field ??=
            ConfigData.Data<EntryDataViewConfigSet>(DataName(ProviderType));

        public Type ProviderType { get; }

        public IConfigurationData ConfigData { get; }

        public ConfigClass(Type providerType, IConfigurationData configData) {
            ProviderType = providerType ?? throw new ArgumentNullException(nameof(providerType));
            ConfigData = configData ?? throw new ArgumentNullException(nameof(configData));
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
            if (config is null) {
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
                get => field ??= new Dictionary<string, EntryDataViewConfigData>();
                set;
            }

            public void Changed() {
                NotifyPropertyChanged(nameof(Set));
            }
        }
    }
}
