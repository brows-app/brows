using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Brows;

internal sealed class TemporaryImageDirectory : IDisposable {
    private static BitmapSource CreateSource(int width, int height) {
        var stride = width * 4;
        var pixels = new byte[stride * height];
        for (var i = 0; i < pixels.Length; i++) {
            pixels[i] = (byte)(i % 251);
        }
        return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
    }

    private FileInfo Save(string name, BitmapEncoder encoder) {
        var path = System.IO.Path.Combine(Path, name);
        using (var stream = File.Create(path)) {
            encoder.Save(stream);
        }
        return new FileInfo(path);
    }

    public string Path { get; }

    public TemporaryImageDirectory() {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Brows.FileSystem.Win32.Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public FileInfo CreatePng(string name, int width, int height) {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(CreateSource(width, height)));
        return Save(name, encoder);
    }

    public FileInfo CreateJpeg(string name, int width, int height, ushort orientation) {
        var metadata = new BitmapMetadata("jpg");
        metadata.SetQuery("System.Photo.Orientation", orientation);
        var encoder = new JpegBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(CreateSource(width, height), null, metadata, null));
        return Save(name, encoder);
    }

    public FileInfo CreateText(string name) {
        var path = System.IO.Path.Combine(Path, name);
        File.WriteAllText(path, "This file is not an image.");
        return new FileInfo(path);
    }

    public void Dispose() {
        Directory.Delete(Path, recursive: true);
    }
}
