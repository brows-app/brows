using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Brows;

internal static class TestShellImage {
    public static BitmapSource Create() {
        var image = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4);
        image.Freeze();
        return image;
    }
}
