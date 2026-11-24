using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Brows.Commands;

/*
 * Reads the EXIF orientation of a frame and describes it as a horizontal
 * mirror followed by a clockwise rotation. Converted pixels always include
 * the orientation, so the output must not carry the original value.
 */
internal static class DrawImageOrientation {
    private const ushort Normal = 1;

    internal const string PropertyName = "System.Photo.Orientation";

    internal static int Read(BitmapFrame frame) {
        ArgumentNullException.ThrowIfNull(frame);
        var decoderHasOrientation = frame.Decoder is JpegBitmapDecoder or TiffBitmapDecoder;
        if (decoderHasOrientation == false) {
            return Normal;
        }
        if (frame.Metadata is not BitmapMetadata metadata) {
            return Normal;
        }
        var value = default(object);
        try {
            value = metadata.GetQuery(PropertyName);
        }
        catch (NotSupportedException) {
            return Normal;
        }
        if (int.TryParse($"{value}", out var orientation) == false) {
            return Normal;
        }
        var orientationIsValid = orientation >= 1 && orientation <= 8;
        if (orientationIsValid == false) {
            return Normal;
        }
        return orientation;
    }

    internal static bool Mirror(int orientation) {
        return orientation is 2 or 4 or 5 or 7;
    }

    internal static int Angle(int orientation) {
        switch (orientation) {
            case 3:
            case 4:
                return 180;
            case 5:
            case 8:
                return 270;
            case 6:
            case 7:
                return 90;
            default:
                return 0;
        }
    }

    internal static TransformGroup Transform(int orientation, int rotate) {
        var transform = new TransformGroup();
        if (Mirror(orientation)) {
            transform.Children.Add(new ScaleTransform(-1, 1));
        }
        var angle = Angle(orientation) + rotate;
        if (angle % 360 != 0) {
            transform.Children.Add(new RotateTransform(angle));
        }
        return transform;
    }

    internal static BitmapMetadata Normalize(BitmapMetadata metadata) {
        if (metadata is null) {
            return null;
        }
        var clone = metadata.Clone();
        try {
            if (clone.GetQuery(PropertyName) is not null) {
                clone.SetQuery(PropertyName, Normal);
            }
        }
        catch (NotSupportedException) {
        }
        return clone;
    }
}
