using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using ComponentModel;
    using Gui;
    using Logger;
    using Threading.Tasks;
    using Translation;

    public abstract class EntryProvider : NotifyPropertyChanged, IEntryProvider {
        private IEntryProviderTarget Target;
        private CancellationTokenSource CancellationTokenSource;

        private ITranslation Translate =>
            Global.Translation;

        private ILog Log =>
            _Log ?? (
            _Log = Logging.For(typeof(EntryProvider)));
        private ILog _Log;

        private TaskHandler TaskHandler =>
            _TaskHandler ?? (
            _TaskHandler = new TaskHandler<EntryProvider>());
        private TaskHandler _TaskHandler;

        protected IEntryBrowser Browser =>
            Target;

        protected virtual Task RefreshAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        protected virtual Task EndAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        protected virtual Task BeginAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public abstract IPanelID PanelID { get; }
        public abstract IReadOnlySet<string> DataKeyDefaults { get; }
        public virtual IReadOnlySet<string> DataKeyOptions => DataKeyDefaults;
        public virtual IReadOnlyDictionary<string, IEntryColumn> DataKeyColumns { get; }
        public virtual IBookmark Bookmark { get; }
        public virtual Image Icon { get; }
        public virtual string Directory { get; }

        public string ParentID {
            get => _ParentID;
            protected set => Change(ref _ParentID, value, nameof(ParentID));
        }
        private string _ParentID;

        public abstract IOperator Operator(IOperatorDeployment deployment);
        public abstract Task<bool> CaseSensitive(CancellationToken cancellationToken);

        public virtual string CreatedID(string createdName) {
            return null;
        }

        public NameValueCollection DataKeyAlias() {
            if (_DataKeyAlias == null) {
                _DataKeyAlias = new NameValueCollection();

                var options = DataKeyOptions;
                foreach (var option in options) {
                    var optionAliases = Translate.Alias(option);
                    if (optionAliases.Length == 0) {
                        _DataKeyAlias.Add(option, option);
                    }
                    else {
                        foreach (var optionAlias in optionAliases) {
                            _DataKeyAlias.Add(option, optionAlias);
                        }
                    }
                }
            }
            return _DataKeyAlias;
        }
        private NameValueCollection _DataKeyAlias;

        public string DataKeyLookup(string alias) {
            foreach (var item in DataKeyLookup(new[] { alias })) {
                return item.ToString();
            }
            return null;
        }

        public IEnumerable DataKeyLookup(params string[] aliases) {
            var options = DataKeyOptions;
            foreach (var option in options) {
                var optionAliases = Translate.Alias(option).Append(option);
                foreach (var optionAlias in optionAliases) {
                    var aliased = aliases.Any(alias => string.Equals(optionAlias, alias, StringComparison.CurrentCultureIgnoreCase));
                    if (aliased) {
                        yield return option;
                        break;
                    }
                }
            }
        }

        public void Begin(IEntryProviderTarget target) {
            if (Log.Info()) {
                Log.Info(nameof(Begin));
            }
            Target = target;
            CancellationTokenSource = new CancellationTokenSource();
            PanelID?.Begin(CancellationTokenSource.Token);
            TaskHandler.Begin(BeginAsync(CancellationTokenSource.Token));
        }

        public void End() {
            if (Log.Info()) {
                Log.Info(nameof(End));
            }
            CancellationTokenSource.Cancel();
            CancellationTokenSource.Dispose();
            Target = null;
            TaskHandler.Begin(EndAsync(CancellationToken.None));
        }

        public void Refresh() {
            if (Log.Info()) {
                Log.Info(nameof(Refresh));
            }
            TaskHandler.Begin(RefreshAsync(CancellationTokenSource.Token));
        }

        public abstract class Of<TEntry> : EntryProvider where TEntry : Entry {
            private readonly List<TEntry> List = new List<TEntry>();

            protected IEnumerable<TEntry> Existing => List;

            protected async Task Provide(TEntry entry, CancellationToken cancellationToken) {
                if (null == entry) throw new ArgumentNullException(nameof(entry));
                List.Add(entry);
                var target = Target;
                if (target != null) {
                    await target.Add(entry, cancellationToken);
                }
            }

            protected async Task Revoke(TEntry entry, CancellationToken cancellationToken) {
                List.Remove(entry);
                var target = Target;
                if (target != null) {
                    await target.Remove(entry, cancellationToken);
                }
            }
        }
    }

    public abstract class EntryProvider<TEntry> : EntryProvider.Of<TEntry> where TEntry : Entry {
    }
}
