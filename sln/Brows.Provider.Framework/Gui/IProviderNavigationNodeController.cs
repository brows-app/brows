using System;

namespace Brows.Gui {
    public interface IProviderNavigationNodeController {
        event EventHandler Loaded;
        event EventHandler Unloaded;
        bool IsLoaded { get; }
    }
}
