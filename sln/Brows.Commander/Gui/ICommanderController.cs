using System;

namespace Brows.Gui {
    public interface ICommanderController {
        event EventHandler Loaded;
        event EventHandler WindowClosed;
        event CommanderInputEventHandler WindowInput;
        event CommanderPressEventHandler WindowPress;
        void CloseWindow();
        object NativeWindow();
        void AddPanel(IPanel panel);
        void RemovePanel(IPanel panel);
    }
}
