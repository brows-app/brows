using System;

namespace Brows.Gui {
    public interface IFindResultController : IController, ICollectionController {
        event EventHandler CurrentChanged;
        FindItem CurrentItem { get; }
    }
}
