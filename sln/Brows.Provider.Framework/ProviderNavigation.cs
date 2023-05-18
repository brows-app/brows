using Brows.Gui;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public abstract class ProviderNavigation : IProviderNavigation, IProviderFocus, IControlled<IProviderNavigationController> {
        private string CurrentID;
        private Provider Provider;

        private void Current(string id) {
            foreach (var node in Root.Descendents.Prepend(Root)) {
                if (node.Is(id)) {
                    node.Expand = true;
                    node.Select = true;
                    Controller?.Current(node);
                    break;
                }
                if (node.PartOf(id)) {
                    node.Expand = true;
                }
            }
            CurrentID = id;
        }

        internal void NodeAdded(ProviderNavigationNode node) {
            if (node == null) return;
            if (node.Is(CurrentID)) {
                node.Expand = true;
                node.Select = true;
                Controller?.Current(node);
                return;
            }
            if (node.PartOf(CurrentID)) {
                node.Expand = true;
            }
        }

        internal async Task<bool> Requested(ProviderNavigationNode node, CancellationToken token) {
            if (null == node) throw new ArgumentNullException(nameof(node));
            var task = Provider?.Change(node.ID, token);
            return task == null
                ? false
                : await task;
        }

        protected ProviderNavigation(ProviderNavigationNode root) {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            Root.Navigation = this;
        }

        protected internal abstract ProviderNavigation For(Provider provider);

        public ProviderNavigationNode Root { get; }

        public void Provided(Provider provider) {
            Provider = provider;
            Current(Provider?.ID);
        }

        bool IProviderFocus.Set() {
            return Controller?.Focus() ?? false;
        }

        bool IProviderFocus.Get() {
            return Controller?.Focused ?? false;
        }

        IProviderNavigationController IControlled<IProviderNavigationController>.Controller {
            set {
                var newValue = value;
                var oldValue = Controller;
                if (oldValue != newValue) {
                    if (oldValue != null) {
                    }
                    if (newValue != null) {
                    }
                    Controller = newValue;
                }
            }
        }
        private IProviderNavigationController Controller;
    }
}
