using Brows.Gui;
using Domore.Logs;
using Domore.Notification;
using Domore.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public abstract class ProviderNavigationNode : Notifier, IProviderNavigationNode, ICommandSourceObject, IControlled<IProviderNavigationNodeController> {
        private static readonly ILog Log = Logging.For(typeof(ProviderNavigationNode));

        private bool AddedChildren;
        private bool AddingChildren;
        private readonly ObservableCollection<ProviderNavigationNode> ChildCollection = new();

        private TaskCache<object> IconCache =>
            _IconCache ?? (
            _IconCache = new(GetIcon));
        private TaskCache<object> _IconCache;

        private void Controller_Loaded(object sender, EventArgs e) {
            if (AddedChildren) {
                return;
            }
            if (AddingChildren) {
                return;
            }
            AddChildren();
        }

        private void Controller_Unloaded(object sender, EventArgs e) {
            Unloaded();
        }

        private async void AddChildren() {
            var token = Token;
            try {
                AddingChildren = true;
                ChildCollection.Clear();
                await foreach (var child in EnumerateChildren(token)) {
                    child.Navigation = Navigation;
                    child.Parent = this;
                    ChildCollection.Add(child);
                    Navigation?.NodeAdded(child);
                }
                AddedChildren = true;
            }
            catch (OperationCanceledException canceled) when (canceled.CancellationToken == token) {
                if (Log.Debug()) {
                    Log.Debug(Log.Join(nameof(AddChildren), nameof(canceled)));
                }
                ChildCollection.Clear();
            }
            catch {
                ChildCollection.Clear();
                throw;
            }
            finally {
                AddingChildren = false;
            }
        }

        private void Collapsed(bool @private) {
            IconCache.Refresh();
            Icon = null;
            Collapsed();
        }

        private void Expanded(bool @private) {
            IconCache.Refresh();
            Icon = null;
            if (AddedChildren == false && AddingChildren == false) {
                AddChildren();
            }
            Expanded();
        }

        private async Task<bool> Requested(CancellationToken token) {
            var task = Navigation?.Requested(this, token);
            return task == null
                ? false
                : await task;
        }

        internal ProviderNavigation Navigation {
            get => _Navigation;
            set => Change(ref _Navigation, value, nameof(Navigation));
        }
        private ProviderNavigation _Navigation;

        protected abstract Task<object> GetIcon(CancellationToken token);
        protected abstract IAsyncEnumerable<ProviderNavigationNode> EnumerateChildren(CancellationToken token);
        protected internal abstract bool PartOf(string id);

        protected virtual void Expanded() {
        }

        protected virtual void Collapsed() {
        }

        protected internal virtual bool Is(string id) {
            return ID == id;
        }

        protected void Refresh() {
            AddChildren();
        }

        protected virtual void Unloaded() {
        }

        public IEnumerable<ProviderNavigationNode> Children => ChildCollection;

        public ProviderNavigationNode Parent {
            get => _Parent;
            private set => Change(ref _Parent, value, nameof(Parent));
        }
        private ProviderNavigationNode _Parent;

        public abstract string ID { get; }
        public abstract string Name { get; }

        public IEnumerable<ProviderNavigationNode> Descendents {
            get {
                foreach (var child in Children) {
                    foreach (var descendent in child.Descendents.Prepend(child)) {
                        yield return descendent;
                    }
                }
            }
        }

        public object Icon {
            get {
                if (_Icon == null) {
                    async void load() {
                        try {
                            Icon = await IconCache.Ready(Token);
                        }
                        catch (Exception ex) {
                            if (Log.Warn()) {
                                Log.Warn(Log.Join(nameof(Icon), nameof(Exception), ID), ex);
                            }
                        }
                    }
                    load();
                }
                return _Icon;
            }
            private set {
                Change(ref _Icon, value, nameof(Icon));
            }
        }
        private object _Icon;

        public bool Select {
            get => _Select;
            set => Change(ref _Select, value, nameof(Select));
        }
        private bool _Select;

        public bool Expand {
            get => _Expand;
            set {
                if (Change(ref _Expand, value, nameof(Expand))) {
                    if (Expand == true) {
                        Expanded(@private: true);
                    }
                    else {
                        Collapsed(@private: true);
                    }
                }
            }
        }
        private bool _Expand;

        public CancellationToken Token {
            get => _Token;
            set => Change(ref _Token, value, nameof(Token));
        }
        private CancellationToken _Token;

        object ICommandSourceObject.Instance =>
            this;

        IEnumerable ICommandSourceObject.Collection =>
            new[] { this };

        IProviderNavigationNodeController IControlled<IProviderNavigationNodeController>.Controller {
            set {
                var newValue = value;
                var oldValue = Controller;
                if (oldValue != newValue) {
                    if (oldValue != null) {
                        oldValue.Loaded -= Controller_Loaded;
                        oldValue.Unloaded -= Controller_Unloaded;
                    }
                    if (newValue != null) {
                        newValue.Loaded += Controller_Loaded;
                        newValue.Unloaded += Controller_Unloaded;
                    }
                    var controller = Controller = newValue;
                    if (controller?.IsLoaded == true) {
                        Controller_Loaded(controller, EventArgs.Empty);
                    }
                }
            }
        }
        private IProviderNavigationNodeController Controller;
    }
}
