using System.Windows;
using System.Windows.Controls;

namespace Brows.Providers;

internal sealed class ProviderTreeViewItemsTreeView : TreeView {
    protected override DependencyObject GetContainerForItemOverride() {
        return new ProviderTreeViewItem();
    }
}
