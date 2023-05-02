using Brows.Config;
using Domore.Logs;
using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public abstract class Provider : Notifier, IProvider, IDisposable {
        private static readonly ILog Log = Logging.For(typeof(Provider));

        private readonly object TokenLocker = new();
        private CancellationTokenSource TokenSource = new();

        private Type EntryType { get; }
        private EntryObservation EntryObservation { get; }

        private Provider(string id, Type entryType, EntryObservation entryObservation) {
            ID = id;
            EntryType = entryType;
            EntryObservation = entryObservation ?? throw new ArgumentNullException(nameof(entryObservation));
            EntryObservation.Provider = this;
        }

        internal IPanel Panel { get; set; }
        internal IImport Import { get; set; }

        internal EntryConfig Config {
            get => _Config ?? (_Config = new());
            set => _Config = value;
        }
        private EntryConfig _Config;

        internal ProviderData Data =>
            _Data ?? (
            _Data = ProviderData.Get(Import, EntryType, DataDefinition));
        private ProviderData _Data;

        internal virtual async Task Init(CancellationToken token) {
            if (Panel.HasProvider(out Provider previous)) {
                var thisType = GetType();
                var previousType = previous.GetType();
                if (previousType == thisType) {
                    var detail = previous.Detail as ProviderDetail;
                    if (detail != null) {
                        Detail = detail.For(this);
                    }
                }
            }
            await Task.CompletedTask;
        }

        internal void End(bool @private) {
            EntryObservation.End();
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

        protected virtual Task<bool> Drop(IPanelDrop data, IOperationProgress progress, CancellationToken token) {
            return Task.FromResult(false);
        }

        protected virtual Task Refresh(CancellationToken token) => Task.CompletedTask;
        protected virtual Task Begin(CancellationToken token) => Task.CompletedTask;
        protected virtual void End() {
        }

        public string ID { get; }
        public virtual string Parent { get; }

        public virtual object Detail {
            get => _Detail;
            set => Change(ref _Detail, value, nameof(Detail));
        }
        private object _Detail;

        public object Observation =>
            EntryObservation;

        public async Task<bool> Change(string id, CancellationToken token) {
            var panel = Panel;
            if (panel == null) return false;
            return await panel.Provide(id, token);
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

        Task<bool> IProvider.Drop(IPanelDrop data, IOperationProgress progress, CancellationToken token) {
            if (Log.Info()) {
                Log.Info(Log.Join(nameof(Drop), ID));
            }
            return Drop(data, progress, token);
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
                await Begin(token);
            }
            catch (Exception ex) {
                if (ex is OperationCanceledException canceled && canceled.CancellationToken == token) {
                    if (Log.Debug()) {
                        Log.Debug(Log.Join(nameof(Begin), nameof(OperationCanceledException), ID));
                    }
                    else {
                        if (Log.Error()) {
                            Log.Error(nameof(Begin), ID, ex);
                        }
                    }
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
            End(@private: true);
            End();
        }

        async void IProvider.Refresh() {
            if (Log.Info()) {
                Log.Info(Log.Join(nameof(Refresh), ID));
            }
            var token = Token;
            try {
                await Refresh(Token);
            }
            catch (Exception ex) {
                if (ex is OperationCanceledException canceled && canceled.CancellationToken == token) {
                    if (Log.Debug()) {
                        Log.Debug(Log.Join(nameof(Refresh), nameof(OperationCanceledException), ID));
                    }
                }
                else {
                    if (Log.Error()) {
                        Log.Error(nameof(Refresh), ID, ex);
                    }
                }
            }
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

            protected virtual void Adding(IReadOnlyCollection<TEntry> entries) {
            }

            protected virtual void Removing(IReadOnlyCollection<TEntry> entries) {
            }

            protected async Task Provide(IReadOnlyCollection<TEntry> entries) {
                if (entries == null) throw new ArgumentNullException(nameof(entries));
                if (entries.Count > 0) {
                    Adding(entries);
                    await EntryObservation.Add(entries);
                }
            }

            protected async Task Provide(TEntry entry) {
                await Provide(new[] { entry });
            }

            protected async Task Revoke(IReadOnlyCollection<TEntry> entries) {
                if (entries == null) throw new ArgumentNullException(nameof(entries));
                if (entries.Count > 0) {
                    Removing(entries);
                    await EntryObservation.Remove(entries);
                }
            }

            protected async Task Revoke(TEntry entry) {
                await Revoke(new[] { entry });
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
                internal sealed override async Task Init(CancellationToken token) {
                    await
                    base.Init(token);
                    base.Config = Config = await Configure.File<TConfig>().Load(token);
                    await EntryObservation.Init(token);
                }

                protected new TConfig Config { get; private set; }

                protected With(string id, int initialCapacity, IEqualityComparer<string> compareID, IEqualityComparer<string> compareName) : base(id, initialCapacity, compareID, compareName) {
                }
            }
        }
    }

    public abstract class Provider<TEntry, TConfig> : Provider.Of<TEntry>.With<TConfig> where TEntry : Entry where TConfig : EntryConfig, new() {
        public Provider(string id, int initialCapacity, IEqualityComparer<string> compareID, IEqualityComparer<string> compareName) : base(id, initialCapacity, compareID, compareName) {
        }

        public Provider(string id) : this(id, 0, null, null) {
        }
    }
}
