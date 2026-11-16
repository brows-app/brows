using System;

namespace Brows.Providers;

public interface IProviderNavigationNodeController {
    event EventHandler Loaded;
    event EventHandler Unloaded;
    bool IsLoaded { get; }
}
