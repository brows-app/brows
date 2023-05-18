using System;

namespace Brows.Gui {
    internal sealed class ProviderNavigationNodeController : Controller<IProviderNavigationNodeController>, IProviderNavigationNodeController {
        public new ProviderTreeViewItem Element { get; }

        public ProviderNavigationNodeController(ProviderTreeViewItem element) : base(element) {
            Element = element ?? throw new ArgumentNullException(nameof(element));
        }

        bool IProviderNavigationNodeController.IsLoaded =>
            Element.IsLoaded;
    }
}
