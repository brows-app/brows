using Brows.Gui;
using System;

namespace Brows.Panels;

public interface IPanelController : IController {
    event EventHandler FocusedChanged;
    event EventHandler SizeChanged;
    event EventHandler Activated;
    event EventHandler Drop;
    IPanelDrop Dropped { get; }
    bool Focused { get; }
    double Width { get; }
    double Height { get; }
}
