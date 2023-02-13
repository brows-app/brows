using Domore.Text;
using System;

namespace Brows.Gui {
    public interface IPreviewConfig {
        DecodedTextOptions TextOptions { get; }
        long? TextFileLengthMax { get; }
        Guid? CLSID(string extension);
        Guid? CLSID(Guid existing);
    }
}
