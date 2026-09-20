using System.Windows;

namespace Brows.Providers;

partial class ProviderTreeViewItem {
    protected override DependencyObject GetContainerForItemOverride() {
        return new ProviderTreeViewItem();
    }

    protected override void OnSelected(RoutedEventArgs e) {
        BringIntoView();
        base.OnSelected(e);
    }

    public ProviderTreeViewItem() {
        InitializeComponent();
        new ProviderNavigationNodeController(this);
    }
}
