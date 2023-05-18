using Brows.Gui;
using System.Windows;

namespace Brows {
    partial class ProviderTreeView {
        protected override DependencyObject GetContainerForItemOverride() {
            return new ProviderTreeViewItem();
        }

        public ProviderTreeView() {
            InitializeComponent();
            new ProviderNavigationController(this);
        }
    }
}
