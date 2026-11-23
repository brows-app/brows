using Domore.Logs;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Brows;

/*
 * Decodes an image file from a stream, scaled to fit a requested size, for
 * files whose thumbnail the Windows Shell cannot provide.
 */
internal static class FileSystemImageDecoder {
    private static readonly ILog Log = Logging.For(typeof(FileSystemImageDecoder));

    private static FileStream Open(FileInfo file) {
        return new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    }

    private static int Orientation(BitmapFrame frame) {
        if (frame.Metadata is not BitmapMetadata metadata) {
            return 1;
        }
        var value = default(object);
        try {
            value = metadata.GetQuery("System.Photo.Orientation");
        }
        catch (NotSupportedException) {
            return 1;
        }
        catch (ArgumentException) {
            return 1;
        }
        if (value is null) {
            return 1;
        }
        if (!int.TryParse($"{value}", out var orientation)) {
            return 1;
        }
        return orientation;
    }

    private static Transform OrientationTransform(int orientation) {
        /*
         * The EXIF orientation describes how the stored pixels must be
         * transformed for display. A TransformGroup applies its children in
         * order, and positive WPF angles rotate clockwise.
         */
        switch (orientation) {
            case 2:
                return new ScaleTransform(-1, 1);
            case 3:
                return new RotateTransform(180);
            case 4:
                return new ScaleTransform(1, -1);
            case 5:
                return new TransformGroup { Children = { new RotateTransform(90), new ScaleTransform(-1, 1) } };
            case 6:
                return new RotateTransform(90);
            case 7:
                return new TransformGroup { Children = { new RotateTransform(270), new ScaleTransform(-1, 1) } };
            case 8:
                return new RotateTransform(270);
            default:
                return null;
        }
    }

    private static BitmapSource DecodeCore(FileInfo file, int width, int height, CancellationToken token) {
        var image = new BitmapImage();
        int orientation;
        /*
         * Read the metadata and the pixels through one handle. A save or sync
         * that replaces the file cannot then pair one version's size and
         * orientation with another version's pixels.
         */
        using (var stream = Open(file)) {
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            if (decoder.Frames.Count == 0) {
                return null;
            }
            var frame = decoder.Frames[0];
            var pixelWidth = frame.PixelWidth;
            var pixelHeight = frame.PixelHeight;
            orientation = Orientation(frame);
            var sizeIsInvalid = pixelWidth <= 0 || pixelHeight <= 0;
            if (sizeIsInvalid) {
                return null;
            }
            if (token.IsCancellationRequested) {
                token.ThrowIfCancellationRequested();
            }
            var swapsSides = orientation >= 5 && orientation <= 8;
            var displayWidth = swapsSides ? pixelHeight : pixelWidth;
            var displayHeight = swapsSides ? pixelWidth : pixelHeight;
            var widthScale = (double)width / displayWidth;
            var heightScale = (double)height / displayHeight;
            var scale = Math.Min(1.0, Math.Min(widthScale, heightScale));

            stream.Position = 0;
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            if (scale < 1) {
                /*
                 * Setting one dimension preserves the aspect ratio, and lets
                 * decoders such as JPEG's scale while decoding. Setting the
                 * stored dimension that limits the scale keeps the other one,
                 * which WPF computes, within its bound.
                 */
                var displayWidthLimits = widthScale <= heightScale;
                var storedWidthLimits = swapsSides ? !displayWidthLimits : displayWidthLimits;
                if (storedWidthLimits) {
                    image.DecodePixelWidth = Math.Max(1, (int)Math.Round(pixelWidth * scale));
                }
                else {
                    image.DecodePixelHeight = Math.Max(1, (int)Math.Round(pixelHeight * scale));
                }
            }
            image.EndInit();
        }
        var source = (BitmapSource)image;
        var transform = OrientationTransform(orientation);
        if (transform is not null) {
            source = new TransformedBitmap(image, transform);
        }
        if (source.CanFreeze) {
            source.Freeze();
        }
        return source;
    }

    /*
     * Decodes the image in a file, scaled down to fit within a width and
     * height and oriented for display. Returns null when the file cannot be
     * decoded as an image.
     */
    public static async Task<BitmapSource> Decode(FileInfo file, int width, int height, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        try {
            return await Task.Run(() => DecodeCore(file, width, height, token), token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
            throw;
        }
        catch (Exception ex) {
            if (Log.Debug()) {
                Log.Debug($"Could not decode an image from {file.FullName}", ex);
            }
            return null;
        }
    }
}
