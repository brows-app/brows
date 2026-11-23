using Brows.Gui;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Providers;

/// <summary>
/// Keeps a navigation tree in step with the provider shown by a panel.
/// </summary>
public abstract class ProviderNavigation : IProviderNavigation,
                                           IProviderFocus,
                                           IControlled<IProviderNavigationController> {
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

    internal Task<bool> Requested(ProviderNavigationNode node, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(node);
        var task = Provider?.Change(node.ID, token);
        return task ?? Task.FromResult(false);
    }

    /// <summary>
    /// Initializes navigation with a root node.
    /// </summary>
    /// <param name="root">
    /// The root of the navigation tree.
    /// </param>
    protected ProviderNavigation(ProviderNavigationNode root) {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        Root.Navigation = this;
    }

    /// <summary>
    /// Gets navigation suitable for another provider.
    /// </summary>
    /// <param name="provider">
    /// The provider that needs navigation.
    /// </param>
    /// <returns>
    /// The navigation model to use for the supplied provider.
    /// </returns>
    protected internal abstract ProviderNavigation For(Provider provider);

    /// <summary>
    /// Gets the root node of the navigation tree.
    /// </summary>
    public ProviderNavigationNode Root { get; }

    /// <summary>
    /// Updates the navigation selection to reflect the provider shown in the panel.
    /// </summary>
    /// <param name="provider">
    /// The current provider, or null when the provider has ended.
    /// </param>
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
