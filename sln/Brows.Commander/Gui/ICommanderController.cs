using System;

namespace Brows.Gui {
    public interface ICommanderController {
        event EventHandler Loaded;
        event EventHandler WindowClosed;
        event InputEventHandler WindowInput;
        event GestureEventHandler WindowGesture;
        void CloseWindow();
        object NativeWindow();
        void AddPanel(IPanel panel);
        void RemovePanel(IPanel panel);
    }
}
