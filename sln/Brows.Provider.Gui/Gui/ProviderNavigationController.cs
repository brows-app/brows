using System;

namespace Brows.Gui {
    internal sealed class ProviderNavigationController : Controller<IProviderNavigationController>, IProviderNavigationController {
        public new ProviderTreeView Element { get; }

        public ProviderNavigationController(ProviderTreeView element) : base(element) {
            Element = element ?? throw new ArgumentNullException(nameof(element));
        }

        bool IProviderNavigationController.Focused =>
            Element.IsKeyboardFocusWithin;

        bool IProviderNavigationController.Current(ProviderNavigationNode node) {
            return Element.Items.MoveCurrentTo(node);
        }

        ProviderNavigationNode IProviderNavigationController.Current() {
            return Element.Items.CurrentItem as ProviderNavigationNode;
        }

        bool IProviderNavigationController.Focus() {
            return Element.Focus();
        }
    }
}
