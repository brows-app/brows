using System;
using System.Windows.Media.Imaging;

namespace Brows.Commands {
    internal static class DrawImageRotation {
        public static double Angle(BitmapFrame frame) {
            if (frame is null) throw new ArgumentNullException(nameof(frame));
            if (frame.Decoder is not JpegBitmapDecoder) {
                return 0;
            }
            var metadata = frame.Metadata as BitmapMetadata;
            if (metadata == null) {
                return 0;
            }
            var orientation = default(object);
            try {
                orientation = metadata.GetQuery("System.Photo.Orientation");
            }
            catch (NotSupportedException) {
                return 0;
            }
            if (orientation == null) {
                return 0;
            }
            if (int.TryParse($"{orientation}", out var i) == false) {
                return 0;
            }
            switch (i) {
                case 6:
                    return 90;
                case 3:
                    return 180;
                case 8:
                    return 270;
                default:
                    return 0;
            }
        }
    }
}
