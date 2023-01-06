using System;

namespace Brows.Gui {
    public interface IPreviewConfig {
        Guid? CLSID(string extension);
        Guid? CLSID(Guid existing);
    }
}
