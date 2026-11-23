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

    /// <summary>
    /// Gets additional data definitions for this provider, or null when none are supplied.
    /// </summary>
    protected virtual IReadOnlyCollection<IEntryDataDefinition> DataDefinition =>
        null;

    /// <summary>
    /// Gets the data keys currently displayed for the provider's entries.
    /// </summary>
    protected IEnumerable<string> ObservedKeys =>
        EntryObservation.Keys;

    /// <summary>
    /// Handles the start of a drag operation for selected entries.
    /// </summary>
    /// <param name="source">
    /// The object from which dragging started.
    /// </param>
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

    /// <summary>
    /// Provides access to the entries and selection of a particular entry type.
    /// </summary>
    /// <typeparam name="TEntry">
    /// The type of entry supplied by the provider.
    /// </typeparam>
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

        /// <summary>
        /// Gets the entries currently supplied by the provider.
        /// </summary>
        protected IReadOnlyList<TEntry> Provided =>
            EntryObservation.Items;

        /// <summary>
        /// Gets the entries currently selected in the provider.
        /// </summary>
        protected IEnumerable<TEntry> Selected =>
            EntryObservation.Selected.Cast<TEntry>();

        /// <summary>
        /// Gets the current entry sorting settings.
        /// </summary>
        protected IEntrySorting Sorting =>
            EntryObservation.DataView.Sorting;

        /// <summary>
        /// Responds before entries are added to the provider.
        /// </summary>
        /// <param name="entries">
        /// The entries being added to the provider.
        /// </param>
        protected virtual void Adding(IReadOnlyCollection<TEntry> entries) {
        }

        /// <summary>
        /// Responds before entries are removed from the provider.
        /// </summary>
        /// <param name="entries">
        /// The entries being removed from the provider.
        /// </param>
        protected virtual void Removing(IReadOnlyCollection<TEntry> entries) {
        }

        /// <summary>
        /// Adds entries to the provider.
        /// </summary>
        /// <param name="entries">
        /// The entries to add.
        /// </param>
        /// <param name="token">
        /// A token used to cancel the operation.
        /// </param>
        /// <returns>
        /// A task that completes when the entries have been added.
        /// </returns>
        protected async Task Provide(IReadOnlyCollection<TEntry> entries, CancellationToken token) {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (entries.Count > 0) {
                Adding(entries);
                using (LinkedTokenSource(token, out var t)) {
                    await EntryObservation.Add(entries, t);
                }
            }
        }

        /// <summary>
        /// Adds an entry to the provider.
        /// </summary>
        /// <param name="entry">
        /// The entry to add.
        /// </param>
        /// <param name="token">
        /// A token used to cancel the operation.
        /// </param>
        /// <returns>
        /// A task that completes when the entry has been added.
        /// </returns>
        protected async Task Provide(TEntry entry, CancellationToken token) {
            using (LinkedTokenSource(token, out var t)) {
                await Provide([entry], t);
            }
        }

        /// <summary>
        /// Removes entries from the provider.
        /// </summary>
        /// <param name="entries">
        /// The entries to remove.
        /// </param>
        /// <param name="token">
        /// A token used to cancel the operation.
        /// </param>
        /// <returns>
        /// A task that completes when the entries have been removed.
        /// </returns>
        protected async Task Revoke(IReadOnlyCollection<TEntry> entries, CancellationToken token) {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (entries.Count > 0) {
                Removing(entries);
                using (LinkedTokenSource(token, out var t)) {
                    await EntryObservation.Remove(entries, t);
                }
            }
        }

        /// <summary>
        /// Removes an entry from the provider.
        /// </summary>
        /// <param name="entry">
        /// The entry to remove.
        /// </param>
        /// <param name="token">
        /// A token used to cancel the operation.
        /// </param>
        /// <returns>
        /// A task that completes when the entry has been removed.
        /// </returns>
        protected async Task Revoke(TEntry entry, CancellationToken token) {
            using (LinkedTokenSource(token, out var t)) {
                await Revoke([entry], t);
            }
        }

        /// <summary>
        /// Finds a provided entry by identifier or name.
        /// </summary>
        /// <param name="id">
        /// The entry identifier, or null to search by name only.
        /// </param>
        /// <param name="name">
        /// The entry name, or null to search by identifier only.
        /// </param>
        /// <returns>
        /// The matching entry, or null. When both arguments are supplied, the entry must match both.
        /// </returns>
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

        /// <summary>
        /// Adds typed entry configuration to a provider of entries.
        /// </summary>
        /// <typeparam name="TConfig">
        /// The configuration type used by the provider.
        /// </typeparam>
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

            /// <summary>
            /// Gets the provider's entry configuration.
            /// </summary>
            protected new TConfig Config { get; private set; }
        }
    }
}
