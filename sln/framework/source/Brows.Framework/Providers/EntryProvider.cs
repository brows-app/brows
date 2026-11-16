using Brows.Conf;
using Brows.Entries;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using IMPORTS = Brows.Imports;

namespace Brows.Providers;

/// <summary>
/// A provider whose content is a list of entries.
/// </summary>
/// <remarks>
/// A provider of entries derives from <see cref="Provider{TEntry, TConfig}"/>.
/// </remarks>
public abstract class EntryProvider : Provider, IEntryProvider {
    private Type EntryType { get; }
    private EntryObservation EntryObservation { get; }

    private EntryProvider(string id, Type entryType, EntryObservation entryObservation) : base(id) {
        EntryType = entryType ?? throw new ArgumentNullException(nameof(entryType));
        EntryObservation = entryObservation ?? throw new ArgumentNullException(nameof(entryObservation));
        EntryObservation.Provider = this;
    }

    private protected sealed override bool Focus() {
        return EntryObservation.Focus();
    }

    private protected sealed override void EndProvided() {
        EntryObservation.End();
    }

    internal EntryConfig Config {
        get => field ?? throw new InvalidOperationException("Entry config is null.");
        private set;
    }

    internal ProviderData Data => field ??=
        ProviderData.Get(Import, EntryType, DataDefinition);

    internal IEnumerable Selection =>
        EntryObservation.Selected;

    protected virtual IReadOnlyCollection<IEntryDataDefinition> DataDefinition =>
        null;

    protected IEnumerable<string> ObservedKeys =>
        EntryObservation.Keys;

    protected internal virtual void DragSelected(object source) {
    }

    /// <summary>
    /// Gets the observation of the provided entries.
    /// </summary>
    public object Observation =>
        EntryObservation;

    IEntryObservation IEntryProvider.Observation =>
        EntryObservation;

    IEntryDataDefinitionSet IEntryProvider.Data =>
        Data.Definition;

    public abstract class Of<TEntry> : EntryProvider where TEntry : Entry {
        private new EntryObservation<TEntry> EntryObservation { get; }

        private Of(string id, EntryObservation<TEntry> entryObservation) : base(id, typeof(TEntry), entryObservation) {
            EntryObservation = entryObservation ?? throw new ArgumentNullException(nameof(entryObservation));
        }

        private Of(string id,
                   int initialCapacity,
                   IEqualityComparer<string> compareID,
                   IEqualityComparer<string> compareName)
        : this(id, new(initialCapacity, compareID, compareName)) {
        }

        protected IReadOnlyList<TEntry> Provided =>
            EntryObservation.Items;

        protected IEnumerable<TEntry> Selected =>
            EntryObservation.Selected.Cast<TEntry>();

        protected IEntrySorting Sorting =>
            EntryObservation.DataView.Sorting;

        protected virtual void Adding(IReadOnlyCollection<TEntry> entries) {
        }

        protected virtual void Removing(IReadOnlyCollection<TEntry> entries) {
        }

        protected async Task Provide(IReadOnlyCollection<TEntry> entries, CancellationToken token) {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (entries.Count > 0) {
                Adding(entries);
                using (LinkedTokenSource(token, out var t)) {
                    await EntryObservation.Add(entries, t);
                }
            }
        }

        protected async Task Provide(TEntry entry, CancellationToken token) {
            using (LinkedTokenSource(token, out var t)) {
                await Provide([entry], t);
            }
        }

        protected async Task Revoke(IReadOnlyCollection<TEntry> entries, CancellationToken token) {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (entries.Count > 0) {
                Removing(entries);
                using (LinkedTokenSource(token, out var t)) {
                    await EntryObservation.Remove(entries, t);
                }
            }
        }

        protected async Task Revoke(TEntry entry, CancellationToken token) {
            using (LinkedTokenSource(token, out var t)) {
                await Revoke([entry], t);
            }
        }

        protected TEntry Lookup(string id = null, string name = null) {
            if (id != null) {
                var entry = EntryObservation.LookupID(id);
                if (entry != null) {
                    var nameMatches = name == null || (name == entry.Name);
                    if (nameMatches) {
                        return entry;
                    }
                }
                return null;
            }
            if (name != null) {
                var entry = EntryObservation.LookupName(name);
                if (entry != null) {
                    return entry;
                }
            }
            return null;
        }

        public abstract class With<TConfig> : Of<TEntry> where TConfig : EntryConfig, new() {
            private readonly IConfiguration<TConfig> ConfigFile;

            private protected With(string id,
                                   int initialCapacity,
                                   IEqualityComparer<string> compareID,
                                   IEqualityComparer<string> compareName)
            : base(id, initialCapacity, compareID, compareName) {
                /*
                 * Imports are not composed when a provider is constructed, so
                 * fall back to an instance whose config cache is shared with
                 * the composed instance.
                 */
                var configFile = IMPORTS.Current?.Find<IConfigurationFile>(
                    throwIfNotFound: false,
                    throwIfNotReady: false) ?? new ConfigurationFile();
                ConfigFile = configFile.File<TConfig>();
                base.Config = Config = ConfigFile.Loaded;
            }

            internal sealed override async Task Init(CancellationToken token) {
                await base.Init(token);
                using (LinkedTokenSource(token, out var t)) {
                    base.Config = (Config ??= await ConfigFile.Load(t));
                    await EntryObservation.Init(t);
                }
            }

            protected new TConfig Config { get; private set; }
        }
    }
}
