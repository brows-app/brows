using Brows.Windows.Controls;
using System.Windows;
using System.Windows.Controls;

namespace Brows.Providers;

public sealed class ProviderTreeView :
    ModelView<ProviderNavigation, IProviderNavigationController>,
    IProviderNavigationController {
    private ProviderTreeViewItemsTreeView _TreeView;

    public override void OnApplyTemplate() {
        base.OnApplyTemplate();
        _TreeView = GetTemplateChild("PART_Tree") as ProviderTreeViewItemsTreeView;
    }

    bool IProviderNavigationController.Focused {
        get => _TreeView?.IsKeyboardFocusWithin ?? false;
    }

    bool IProviderNavigationController.Current(ProviderNavigationNode node) {
        var treeView = _TreeView;
        if (treeView is null) {
            return false;
        }
        return treeView.Items.MoveCurrentTo(node);
    }

    ProviderNavigationNode IProviderNavigationController.Current() {
        return _TreeView?.Items.CurrentItem as ProviderNavigationNode;
    }

    bool IProviderNavigationController.Focus() {
        return _TreeView?.Focus() ?? false;
    }
}
