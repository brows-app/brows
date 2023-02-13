using Domore.Logs;
using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Gui;
    using Threading.Tasks;

    public abstract class EntryProvider : Notifier, IEntryProvider {
        private static readonly ILog Log = Logging.For(typeof(EntryProvider));

        private IEntryProviderTarget Target;
        private CancellationTokenSource CancellationTokenSource;

        private TaskHandler TaskHandler =>
            _TaskHandler ?? (
            _TaskHandler = new TaskHandler<EntryProvider>());
        private TaskHandler _TaskHandler;

        private EntryProviderDataKey DataKey =>
            _DataKey ?? (
            _DataKey = new(DataKeyOptions));
        private EntryProviderDataKey _DataKey;

        internal abstract Task Init(CancellationToken cancellationToken, bool @private);

        protected IEntryBrowser Browser =>
            Target;

        protected CancellationToken CancellationToken =>
            CancellationTokenSource?.Token ?? throw new InvalidOperationException();

        protected virtual Task Init(CancellationToken cancellationToken) => Task.CompletedTask;
        protected virtual Task Refresh(CancellationToken cancellationToken) => Task.CompletedTask;
        protected virtual Task End(CancellationToken cancellationToken) => Task.CompletedTask;
        protected virtual Task Begin(CancellationToken cancellationToken) => Task.CompletedTask;

        public abstract IPanelID PanelID { get; }
        public abstract IReadOnlySet<string> DataKeyDefault { get; }
        public virtual IReadOnlySet<string> DataKeyOptions => DataKeyDefault;
        public virtual IReadOnlyDictionary<string, IEntryColumn> DataKeyColumns { get; }
        public virtual IReadOnlyDictionary<string, EntrySortDirection?> DataKeySorting { get; }
        public virtual IBookmark Bookmark { get; }
        public virtual Image Icon { get; }
        public virtual string Directory { get; }

        public string ParentID {
            get => _ParentID;
            protected set => Change(ref _ParentID, value, nameof(ParentID));
        }
        private string _ParentID;

        public abstract IOperator Operator(IOperatorDeployment deployment);
        public abstract ValueTask<bool> CaseSensitive(CancellationToken cancellationToken);

        public virtual string CreatedID(string createdName) {
            return null;
        }

        IReadOnlyDictionary<string, IReadOnlySet<string>> IPanelProvider.DataKeyAlias() {
            return DataKey.Alias;
        }

        string IPanelProvider.DataKeyLookup(string alias) {
            return DataKey.Lookup(alias);
        }

        IReadOnlySet<string> IPanelProvider.DataKeyPossible(string part) {
            return DataKey.Possible(part);
        }

        void IEntryProvider.Begin(IEntryProviderTarget target) {
            if (Log.Info()) {
                Log.Info(nameof(Begin));
            }
            Target = target;
            CancellationTokenSource = new CancellationTokenSource();
            PanelID?.Begin(CancellationToken);
            TaskHandler.Begin(Begin(CancellationToken));
        }

        void IEntryProvider.End() {
            if (Log.Info()) {
                Log.Info(nameof(End));
            }
            using (CancellationTokenSource) {
                CancellationTokenSource?.Cancel();
                CancellationTokenSource = null;
                Target = null;
                TaskHandler.Begin(End(CancellationToken.None));
            }
        }

        void IEntryProvider.Refresh() {
            if (Log.Info()) {
                Log.Info(nameof(Refresh));
            }
            TaskHandler.Begin(Refresh(CancellationToken));
        }

        async Task IEntryProvider.Init(CancellationToken cancellationToken) {
            if (Log.Info()) {
                Log.Info(nameof(Init));
            }
            await Init(cancellationToken, @private: true);
            await Init(cancellationToken);
        }

        public abstract class Of<TEntry> : EntryProvider where TEntry : Entry {
            private EntryProvided<TEntry> Provision;

            internal sealed override async Task Init(CancellationToken cancellationToken, bool @private) {
                Provided = Provision = new(await CaseSensitive(cancellationToken));
            }

            protected IEntryProvided<TEntry> Provided { get; private set; }

            protected async Task Provide(IReadOnlyList<TEntry> entries, CancellationToken cancellationToken) {
                Provision.Add(entries);
                await Async.Await(
                    Target?.Add(entries, cancellationToken));
            }

            protected Task Provide(TEntry entry, CancellationToken cancellationToken) {
                return Provide(new[] { entry }, cancellationToken);
            }

            protected async Task Revoke(IReadOnlyList<TEntry> entries, CancellationToken cancellationToken) {
                if (null == entries) throw new ArgumentNullException(nameof(entries));
                Provision.Remove(entries);
                await Async.Await(
                    Target?.Remove(entries, cancellationToken));
            }

            protected Task Revoke(TEntry entry, CancellationToken cancellationToken) {
                return Revoke(new[] { entry }, cancellationToken);
            }
        }
    }

    public abstract class EntryProvider<TEntry> : EntryProvider.Of<TEntry> where TEntry : Entry {
    }
}
