using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Brows.Support;

internal static class DrawImageSample {
    private const string OrientationQuery = "System.Photo.Orientation";

    private static BitmapEncoder Encoder(string path) {
        switch (Path.GetExtension(path).ToLowerInvariant()) {
            case ".jpg":
                return new JpegBitmapEncoder { QualityLevel = 95 };
            case ".png":
                return new PngBitmapEncoder();
            case ".tif":
                return new TiffBitmapEncoder();
            default:
                throw new ArgumentException($"No sample encoder for [{path}].", nameof(path));
        }
    }

    private static string MetadataFormat(string path) {
        switch (Path.GetExtension(path).ToLowerInvariant()) {
            case ".jpg":
                return "jpg";
            case ".tif":
                return "tiff";
            default:
                return null;
        }
    }

    private static bool IsRed(BitmapSource image, int x, int y) {
        var pixel = new byte[4];
        image.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return pixel[2] > 160 && pixel[0] < 96;
    }

    /*
     * Draws a blue image with a red block in its top-left third, so the corner
     * holding the block shows how the image was rotated or mirrored.
     */
    internal static BitmapSource Pixels(int width, int height) {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                var i = (y * width + x) * 4;
                var red = x < width / 3 && y < height / 3;
                pixels[i + 0] = red ? (byte)0 : (byte)255;
                pixels[i + 2] = red ? (byte)255 : (byte)0;
                pixels[i + 3] = 255;
            }
        }
        return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
    }

    internal static FileInfo Create(string path, ushort? orientation, params (int Width, int Height)[] pages) {
        var encoder = Encoder(path);
        foreach (var page in pages) {
            var metadata = default(BitmapMetadata);
            if (orientation is not null) {
                metadata = new BitmapMetadata(MetadataFormat(path));
                metadata.SetQuery(OrientationQuery, orientation.Value);
            }
            encoder.Frames.Add(BitmapFrame.Create(Pixels(page.Width, page.Height), null, metadata, null));
        }
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) {
            encoder.Save(stream);
        }
        return new FileInfo(path);
    }

    /*
     * Writes a PNG of random pixels, which compresses poorly, so the file is
     * large enough to show whether reading it buffers the whole file.
     */
    internal static FileInfo CreateNoise(string path, int width, int height) {
        var pixels = new byte[width * height * 4];
        new Random(1).NextBytes(pixels);
        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) {
            encoder.Save(stream);
        }
        return new FileInfo(path);
    }

    internal static IReadOnlyList<BitmapFrame> Load(string path) {
        var stream = new MemoryStream(File.ReadAllBytes(path));
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        return decoder.Frames.ToList();
    }

    internal static object Orientation(BitmapFrame frame) {
        var metadata = frame.Metadata as BitmapMetadata;
        if (metadata is null) {
            return null;
        }
        try {
            return metadata.GetQuery(OrientationQuery);
        }
        catch (NotSupportedException) {
            return null;
        }
    }

    internal static string RedCorner(BitmapSource image) {
        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var w = converted.PixelWidth;
        var h = converted.PixelHeight;
        var corners = new (string Name, int X, int Y)[] {
            ("TopLeft", w / 6, h / 6),
            ("TopRight", w - 1 - w / 6, h / 6),
            ("BottomLeft", w / 6, h - 1 - h / 6),
            ("BottomRight", w - 1 - w / 6, h - 1 - h / 6)
        };
        var red = corners.Where(corner => IsRed(converted, corner.X, corner.Y)).Select(corner => corner.Name).ToList();
        return string.Join(",", red);
    }
}
