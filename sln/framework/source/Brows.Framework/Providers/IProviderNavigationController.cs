namespace Brows.Providers;

/// <summary>
/// Connects a provider navigation model to its view's focus and current node.
/// </summary>
public interface IProviderNavigationController {
    /// <summary>
    /// Gets whether the navigation view has keyboard focus.
    /// </summary>
    bool Focused { get; }

    /// <summary>
    /// Requests keyboard focus for the navigation view.
    /// </summary>
    /// <returns>
    /// True if the request succeeded; otherwise, false.
    /// </returns>
    bool Focus();

    /// <summary>
    /// Makes a node current in the navigation view.
    /// </summary>
    /// <param name="node">
    /// The node to make current.
    /// </param>
    /// <returns>
    /// True if the current node was set; otherwise, false.
    /// </returns>
    bool Current(ProviderNavigationNode node);

    /// <summary>
    /// Gets the current node in the navigation view.
    /// </summary>
    /// <returns>
    /// The current node, or null when none is current.
    /// </returns>
    ProviderNavigationNode Current();
}
