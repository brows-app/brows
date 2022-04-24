using System;

namespace Brows.Gui {
    public interface ICommanderController : ITriggerController {
        event EventHandler WindowClosed;
        void CloseWindow();
        object NativeWindow();
    }
}
