using System;

namespace Brows.Gui {
    internal interface IFileSystemFindResultController : IController {
        event EventHandler CurrentChanged;
        FoundInInfo CurrentItem { get; }
    }
}
