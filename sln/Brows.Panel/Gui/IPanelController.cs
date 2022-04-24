using System;

namespace Brows.Gui {
    public interface IPanelController : IController {
        event EventHandler FocusedChanged;
        event EventHandler SizeChanged;
        event DropEventHandler Drop;
        bool Focused { get; }
        double Width { get; }
        double Height { get; }
    }
}
