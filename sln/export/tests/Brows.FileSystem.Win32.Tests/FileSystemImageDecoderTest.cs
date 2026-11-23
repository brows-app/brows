using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
internal sealed class FileSystemImageDecoderTest {
    [Test]
    public async Task Decode_WhenImageIsLargerThanRequested_ScalesToFitAndFreezes() {
        using var directory = new TemporaryImageDirectory();
        var file = directory.CreatePng("wide.png", width: 200, height: 100);

        var image = await FileSystemImageDecoder.Decode(file, width: 100, height: 100, CancellationToken.None);

        Assert.That(image, Is.Not.Null);
        using (Assert.EnterMultipleScope()) {
            Assert.That(image.PixelWidth, Is.EqualTo(100));
            Assert.That(image.PixelHeight, Is.EqualTo(50));
            Assert.That(image.IsFrozen, Is.True);
        }
    }

    [Test]
    public async Task Decode_WhenImageIsSmallerThanRequested_KeepsItsSize() {
        using var directory = new TemporaryImageDirectory();
        var file = directory.CreatePng("small.png", width: 40, height: 20);

        var image = await FileSystemImageDecoder.Decode(file, width: 100, height: 100, CancellationToken.None);

        Assert.That(image, Is.Not.Null);
        using (Assert.EnterMultipleScope()) {
            Assert.That(image.PixelWidth, Is.EqualTo(40));
            Assert.That(image.PixelHeight, Is.EqualTo(20));
        }
    }

    [TestCase((ushort)1, 100, 50)]
    [TestCase((ushort)3, 100, 50)]
    [TestCase((ushort)6, 50, 100)]
    [TestCase((ushort)8, 50, 100)]
    public async Task Decode_WhenJpegHasOrientation_OrientsForDisplay(ushort orientation, int width, int height) {
        using var directory = new TemporaryImageDirectory();
        var file = directory.CreateJpeg("photo.jpg", width: 200, height: 100, orientation);

        var image = await FileSystemImageDecoder.Decode(file, width: 100, height: 100, CancellationToken.None);

        Assert.That(image, Is.Not.Null);
        using (Assert.EnterMultipleScope()) {
            Assert.That(image.PixelWidth, Is.EqualTo(width));
            Assert.That(image.PixelHeight, Is.EqualTo(height));
            Assert.That(image.IsFrozen, Is.True);
        }
    }

    [TestCase(5, 300, 128, 96)]
    [TestCase(2, 4096, 1280, 960)]
    [TestCase(300, 5, 96, 128)]
    public async Task Decode_WhenOneSideLimitsTheScale_StaysWithinBothBounds(int pixelWidth,
                                                                            int pixelHeight,
                                                                            int width,
                                                                            int height) {
        using var directory = new TemporaryImageDirectory();
        var file = directory.CreatePng("narrow.png", pixelWidth, pixelHeight);

        var image = await FileSystemImageDecoder.Decode(file, width, height, CancellationToken.None);

        Assert.That(image, Is.Not.Null);
        var heightLimits = (double)height / pixelHeight < (double)width / pixelWidth;
        using (Assert.EnterMultipleScope()) {
            Assert.That(image.PixelWidth, Is.InRange(1, width));
            Assert.That(image.PixelHeight, Is.InRange(1, height));
            Assert.That(heightLimits ? image.PixelHeight : image.PixelWidth, Is.EqualTo(heightLimits ? height : width));
        }
    }

    [Test]
    public async Task Decode_WhenRotatedImageIsNarrow_StaysWithinBothBounds() {
        using var directory = new TemporaryImageDirectory();
        var file = directory.CreateJpeg("narrow.jpg", width: 5, height: 300, orientation: 6);

        var image = await FileSystemImageDecoder.Decode(file, width: 96, height: 128, CancellationToken.None);

        Assert.That(image, Is.Not.Null);
        using (Assert.EnterMultipleScope()) {
            Assert.That(image.PixelWidth, Is.EqualTo(96));
            Assert.That(image.PixelHeight, Is.InRange(1, 128));
        }
    }

    [Test]
    public async Task Decode_WhenFileIsNotAnImage_ReturnsNull() {
        using var directory = new TemporaryImageDirectory();
        var file = directory.CreateText("notes.png");

        var image = await FileSystemImageDecoder.Decode(file, width: 100, height: 100, CancellationToken.None);

        Assert.That(image, Is.Null);
    }

    [Test]
    public void Decode_WhenTokenIsCanceled_ThrowsOperationCanceledException() {
        using var directory = new TemporaryImageDirectory();
        var file = directory.CreatePng("wide.png", width: 200, height: 100);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.That(async () => await FileSystemImageDecoder.Decode(file, width: 100, height: 100, cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }
}
