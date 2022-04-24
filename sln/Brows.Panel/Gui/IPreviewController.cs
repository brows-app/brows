using System;
namespace Brows.Gui {
    public interface IPreviewController {
        event EventHandler SizeChanged;
        double Width { get; }
        double Height { get; }
    }
}
