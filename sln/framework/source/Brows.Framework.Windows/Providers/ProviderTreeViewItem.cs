using System.Windows;
using System.Windows.Controls;

namespace Brows.Providers;

internal sealed class ProviderTreeViewItem : TreeViewItem {
    protected override DependencyObject GetContainerForItemOverride() {
        return new ProviderTreeViewItem();
    }

    protected override void OnSelected(RoutedEventArgs e) {
        BringIntoView();
        base.OnSelected(e);
    }

    public ProviderTreeViewItem() {
        new ProviderNavigationNodeController(this);
    }
}
