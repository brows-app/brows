using System;

namespace Brows.Gui {
    public interface ICommanderController {
        event EventHandler Loaded;
        event EventHandler WindowClosed;
        event EventHandler WindowClosing;
        event InputEventHandler WindowInput;
        event GestureEventHandler WindowGesture;
        void CloseWindow();
        object NativeWindow();
    }
}
