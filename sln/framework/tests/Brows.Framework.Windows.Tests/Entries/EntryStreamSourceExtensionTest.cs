using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Brows.Entries;

[TestFixture]
internal sealed class EntryStreamSourceExtensionTest {
    private static byte[] Encode(BitmapEncoder encoder, int width, int height) {
        var stride = width * 4;
        var pixels = new byte[stride * height];
        for (var i = 0; i < pixels.Length; i++) {
            pixels[i] = (byte)(i % 251);
        }
        var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static BitmapEncoder Encoder(string format) {
        switch (format) {
            case "bmp":
                return new BmpBitmapEncoder();
            case "gif":
                return new GifBitmapEncoder();
            case "jpg":
                return new JpegBitmapEncoder();
            case "png":
                return new PngBitmapEncoder();
            case "tif":
                return new TiffBitmapEncoder();
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }

    [TestCase("bmp")]
    [TestCase("gif")]
    [TestCase("jpg")]
    [TestCase("png")]
    [TestCase("tif")]
    public async Task Image_WhenSourceIsEncodedImage_DecodesFrozenImage(string format) {
        var source = new BytesSource(Encode(Encoder(format), width: 40, height: 20));

        var image = await source.Image(CancellationToken.None);

        Assert.That(image, Is.Not.Null);
        using (Assert.EnterMultipleScope()) {
            Assert.That(image.PixelWidth, Is.EqualTo(40));
            Assert.That(image.PixelHeight, Is.EqualTo(20));
            Assert.That(image.IsFrozen, Is.True);
        }
    }

    [Test]
    public async Task Image_WhenSourceHasNoStream_ReturnsNull() {
        var source = new BytesSource(null);

        var image = await source.Image(CancellationToken.None);

        Assert.That(image, Is.Null);
    }

    private sealed class BytesSource : IEntryStreamSource {
        private readonly byte[] Bytes;

        public string EntryID => null;
        public string EntryName => null;
        public string RelativePath => null;
        public string SourceFile => null;
        public string SourceDirectory => null;
        public long StreamLength => Bytes?.Length ?? 0;
        public Stream Stream { get; private set; }

        public BytesSource(byte[] bytes) {
            Bytes = bytes;
        }

        public Task<IEntryStreamReady> StreamReady(CancellationToken token) {
            Stream = Bytes is null
                ? null
                : new MemoryStream(Bytes, writable: false);
            return Task.FromResult<IEntryStreamReady>(new EntryStreamReady());
        }
    }
}
