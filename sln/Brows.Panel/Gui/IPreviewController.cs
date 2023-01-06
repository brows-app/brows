using System;

namespace Brows.Gui {
    public interface IPreviewController {
        Func<IPreviewConfig> Config { set; }
        void Refresh();
    }
}
