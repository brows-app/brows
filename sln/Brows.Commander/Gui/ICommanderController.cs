using System;

namespace Brows.Gui {
    using Triggers;

    public interface ICommanderController {
        event EventHandler Loaded;
        event EventHandler WindowClosed;
        event InputEventHandler WindowInput;
        event KeyboardKeyEventHandler WindowKeyboardKeyDown;
        void CloseWindow();
        object NativeWindow();
        void AddPanel(IPanel panel);
        void RemovePanel(IPanel panel);
    }
}
