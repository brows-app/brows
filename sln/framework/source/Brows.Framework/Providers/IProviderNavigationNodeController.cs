using System;

namespace Brows.Providers;

/// <summary>
/// Reports whether a navigation node's view is loaded.
/// </summary>
public interface IProviderNavigationNodeController {
    /// <summary>
    /// Occurs when the node's view is loaded.
    /// </summary>
    event EventHandler Loaded;

    /// <summary>
    /// Occurs when the node's view is unloaded.
    /// </summary>
    event EventHandler Unloaded;

    /// <summary>
    /// Gets whether the node's view is loaded.
    /// </summary>
    bool IsLoaded { get; }
}
