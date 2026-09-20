using System.Windows;

namespace Brows.Providers;

partial class ProviderTreeView {
    protected override DependencyObject GetContainerForItemOverride() {
        return new ProviderTreeViewItem();
    }

    public ProviderTreeView() {
        InitializeComponent();
        new ProviderNavigationController(this);
    }
}
