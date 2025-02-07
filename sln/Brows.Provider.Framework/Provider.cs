using Brows.Config;
using Domore.Logs;
using Domore.Notification;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public abstract class Provider : Notifier, IProvider, IDisposable {
        private static readonly ILog Log = Logging.For(typeof(Provider));

        private readonly object TokenLocker = new();
        private volatile CancellationTokenSource TokenSource = new();

        private Type EntryType { get; }
        private EntryObservation EntryObservation { get; }

        private Provider(string id, Type entryType, EntryObservation entryObservation) {
            ID = id;
            EntryType = entryType;
            EntryObservation = entryObservation ?? throw new ArgumentNullException(nameof(entryObservation));
            EntryObservation.Provider = this;
        }

        private Task Begin(CancellationToken token, bool begun) {
            if (token.IsCancellationRequested) {
                return Task.FromCanceled(token);
            }
            if (begun) {
                Navigation?.Provided(this);
            }
            return Task.CompletedTask;
        }

        private void End(bool ended) {
            if (ended == false) {
                Navigation?.Provided(null);
                EntryObservation.End();
            }
        }

        private CancellationTokenSource LinkedTokenSource(CancellationToken other, out CancellationToken token) {
            var t = Token;
            if (t == other) {
                token = t;
                return null;
            }
            if (CancellationToken.None == other) {
                token = t;
                return null;
            }
            var
            source = CancellationTokenSource.CreateLinkedTokenSource(t, other);
            token = source.Token;
            return source;
        }

        internal IPanel Panel { get; set; }
        internal IImport Import { get; set; }

        internal EntryConfig Config {
            get => _Config ?? throw new InvalidOperationException("Entry config is null.");
            private set => _Config = value;
        }
        private EntryConfig _Config;

        internal ProviderData Data => _Data ??= ProviderData.Get(Import, EntryType, DataDefinition);
        private ProviderData _Data;

        internal IEnumerable Selection =>
            EntryObservation.Selected;

        internal virtual Task Init(CancellationToken token) {
            using (LinkedTokenSource(token, out var t)) {
                if (t.IsCancellationRequested) {
                    return Task.FromCanceled(t);
                }
                if (Panel.HasProvider(out Provider previous)) {
                    var navigation = previous.Navigation;
                    if (navigation != null) {
                        Navigation = navigation.For(this);
                    }
                    var detail = previous.Detail;
                    if (detail != null) {
                        foreach (var key in detail.Keys) {
                            Detail[key] = detail[key]?.For(this);
                        }
                    }
                }
            }
            return Task.CompletedTask;
        }

        protected virtual IReadOnlyCollection<IEntryDataDefinition> DataDefinition =>
            null;

        protected IEnumerable<string> ObservedKeys =>
            EntryObservation.Keys;

        protected internal CancellationToken Token {
            get {
                if (TokenSource != null) {
                    lock (TokenLocker) {
                        if (TokenSource != null) {
                            return TokenSource.Token;
                        }
                    }
                }
                return new CancellationToken(canceled: true);
            }
        }

        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                if (TokenSource != null) {
                    lock (TokenLocker) {
                        var tokenSource = TokenSource;
                        if (tokenSource != null) {
                            TokenSource = null;
                            tokenSource.Dispose();
                        }
                    }
                }
            }
        }

        protected internal virtual void DragSelected(object source) {
        }

        protected virtual Task<bool> Take(IMessage message, CancellationToken token) {
            return Task.FromResult(false);
        }

        protected virtual Task<bool> Drop(IPanelDrop data, IOperationProgress progress, CancellationToken token) {
            return Task.FromResult(false);
        }

        protected virtual Task Refresh(CancellationToken token) => Task.CompletedTask;
        protected virtual Task Begin(CancellationToken token) => Task.CompletedTask;
        protected virtual void End() {
        }

        protected async Task<SecureString> GetSecret(string promptFormat, IEnumerable<string> promptArgs, CancellationToken token) {
            var panel = Panel;
            if (panel == null) {
                return null;
            }
            return await panel.GetSecret(promptFormat, promptArgs, token).ConfigureAwait(false);
        }

        public string ID { get; }
        public virtual string Parent { get; }

        public ProviderDetailSet Detail =>
            _Detail ?? (
            _Detail = new());
        private ProviderDetailSet _Detail;

        public ProviderNavigation Navigation {
            get => _Navigation;
            set => Change(ref _Navigation, value, nameof(Navigation));
        }
        private ProviderNavigation _Navigation;

        public object Observation =>
            EntryObservation;

        public async Task<bool> Change(string id, CancellationToken token) {
            var panel = Panel;
            if (panel == null) return false;
            return await panel.Provide(id, token).ConfigureAwait(false);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~Provider() {
            Dispose(false);
        }

        IEntryObservation IProvider.Observation =>
            EntryObservation;

        IEntryDataDefinitionSet IProvider.Data =>
            Data.Definition;

        async Task IProvider.Refresh(CancellationToken token) {
            using (LinkedTokenSource(token, out var t)) {
                var task = Refresh(t);
                if (task != null) {
                    await task.ConfigureAwait(false);
                }
            }
        }

        async Task<bool> IProvider.Take(IMessage message, CancellationToken token) {
            using (LinkedTokenSource(token, out var t)) {
                var task = Take(message, t);
                if (task != null) {
                    return await task.ConfigureAwait(false);
                }
                return false;
            }
        }

        async Task<bool> IProvider.Drop(IPanelDrop data, IOperationProgress progress, CancellationToken token) {
            if (Log.Info()) {
                Log.Info(Log.Join(nameof(Drop), ID));
            }
            using (LinkedTokenSource(token, out var t)) {
                var task = Drop(data, progress, t);
                if (task != null) {
                    return await task.ConfigureAwait(false);
                }
                return false;
            }
        }

        TExport IProvider.Import<TExport>() {
            var import = Import;
            if (import == null) {
                return default;
            }
            var type = GetType();
            var importForType = import.For(type);
            var exportForType = importForType.Get<TExport>();
            return exportForType;
        }

        async void IProvider.Begin() {
            if (Log.Info()) {
                Log.Info(Log.Join(nameof(Begin), ID));
            }
            var token = Token;
            try {
                await Begin(token, begun: false);
                await Begin(token);
                await Begin(token, begun: true);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) {
                if (Log.Debug()) {
                    Log.Debug(Log.Join(nameof(Begin), nameof(OperationCanceledException), ID));
                }
            }
            catch (Exception ex) {
                if (Log.Error()) {
                    Log.Error(nameof(Begin), ID, ex);
                }
            }
        }

        void IProvider.End() {
            if (Log.Info()) {
                Log.Info(Log.Join(nameof(End), ID));
            }
            if (TokenSource != null) {
                lock (TokenLocker) {
                    if (TokenSource != null) {
                        TokenSource.Cancel();
                    }
                }
            }
            Panel = null;
            Import = null;
            End(ended: false);
            End();
            End(ended: true);
        }

        public abstract class Of<TEntry> : Provider where TEntry : Entry {
            private new EntryObservation<TEntry> EntryObservation { get; }

            private Of(string id, EntryObservation<TEntry> entryObservation) : base(id, typeof(TEntry), entryObservation) {
                EntryObservation = entryObservation ?? throw new ArgumentNullException(nameof(entryObservation));
            }

            private Of(string id, int initialCapacity, IEqualityComparer<string> compareID, IEqualityComparer<string> compareName) : this(id, new EntryObservation<TEntry>(initialCapacity, compareID, compareName)) {
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
                        if (name == null || (name == entry.Name)) {
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
                private readonly IConfig<TConfig> ConfigFile;

                private protected With(string id, int initialCapacity, IEqualityComparer<string> compareID, IEqualityComparer<string> compareName) : base(id, initialCapacity, compareID, compareName) {
                    ConfigFile = Configure.File<TConfig>();
                    base.Config = Config = ConfigFile.Loaded;
                }

                internal sealed override async Task Init(CancellationToken token) {
                    await base.Init(token).ConfigureAwait(false);
                    using (LinkedTokenSource(token, out var t)) {
                        base.Config = (Config ??= await ConfigFile.Load(t).ConfigureAwait(false));
                        await EntryObservation.Init(t).ConfigureAwait(false);
                    }
                }

                protected new TConfig Config { get; private set; }
            }
        }
    }

    public abstract class Provider<TEntry, TConfig> : Provider.Of<TEntry>.With<TConfig> where TEntry : Entry where TConfig : EntryConfig, new() {
        protected Provider(string id, int initialCapacity, IEqualityComparer<string> compareID, IEqualityComparer<string> compareName) : base(id, initialCapacity, compareID, compareName) {
        }

        protected Provider(string id) : this(id, 0, null, null) {
        }
    }
}
