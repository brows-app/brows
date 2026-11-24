using Brows.Support;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Brows.Commands;

[TestFixture]
[Apartment(ApartmentState.STA)]
internal sealed class DrawImageFileTest {
    private TempDirectory Temp;

    private string PathOf(string name) {
        return Path.Combine(Temp.Root.FullName, name);
    }

    private FileInfo Sample(string name, ushort? orientation = null, int width = 120, int height = 80) {
        return DrawImageSample.Create(PathOf(name), orientation, (width, height));
    }

    private static DrawImageFile Drawing(FileInfo source, DrawImage.Parameter parameter,
                                         KeyValuePair<FileInfo, string>[] requests) {
        var overwrite = parameter.Overwrite == true;
        var targets = DrawImageTargets.Plan(requests, overwrite, null, CancellationToken.None).GetAwaiter().GetResult();
        return new DrawImageFile(source, parameter, targets);
    }

    private static string Save(DrawImageFile draw) {
        using (draw) {
            return draw.Save();
        }
    }

    private static string Convert(FileInfo source, string output, DrawImage.Parameter parameter) {
        var requests = new[] { new KeyValuePair<FileInfo, string>(source, output) };
        return Save(Drawing(source, parameter, requests));
    }

    [SetUp]
    public void SetUp() {
        Temp = new TempDirectory();
    }

    [TearDown]
    public void TearDown() {
        Temp.Dispose();
    }

    [Test]
    public void Save_MultiPageTiff_KeepsEveryPageInOrder() {
        var source = DrawImageSample.Create(PathOf("pages.tif"), null, (120, 80), (60, 40));
        var output = Convert(source, PathOf("pages-rotated.tif"), new DrawImage.Parameter { Rotate = 90 });
        var frames = DrawImageSample.Load(output);
        Assert.That(frames, Has.Count.EqualTo(2));
        using (Assert.EnterMultipleScope()) {
            Assert.That((frames[0].PixelWidth, frames[0].PixelHeight), Is.EqualTo((80, 120)));
            Assert.That((frames[1].PixelWidth, frames[1].PixelHeight), Is.EqualTo((40, 60)));
        }
    }

    [Test]
    public void Save_InPlaceWithOverwrite_ReplacesSource() {
        var source = Sample("source.png");
        var parameter = new DrawImage.Parameter { Width = 60, Overwrite = true };
        var output = Convert(source, source.FullName, parameter);
        var frame = DrawImageSample.Load(output)[0];
        using (Assert.EnterMultipleScope()) {
            Assert.That(output, Is.EqualTo(source.FullName));
            Assert.That((frame.PixelWidth, frame.PixelHeight), Is.EqualTo((60, 40)));
            Assert.That(Temp.Root.GetFiles(), Has.Length.EqualTo(1));
        }
    }

    [Test]
    public void Save_DistinctOutput_ReleasesSource() {
        var source = Sample("source.png");
        Convert(source, PathOf("output.png"), new DrawImage.Parameter { Width = 60 });
        Assert.That(() => {
            using (new FileStream(source.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
            }
        }, Throws.Nothing);
    }

    [Test]
    public void Save_OverLongerFile_LeavesNoStaleBytes() {
        var source = Sample("source.png");
        var fresh = Convert(source, PathOf("fresh.png"), new DrawImage.Parameter());
        var destination = PathOf("destination.png");
        File.WriteAllBytes(destination, new byte[100000]);
        var output = Convert(source, destination, new DrawImage.Parameter { Overwrite = true });
        Assert.That(File.ReadAllBytes(output), Is.EqualTo(File.ReadAllBytes(fresh)));
    }

    [Test]
    public void Save_RotatedJpegToTiffWithTransfer_DoesNotRotateTwice() {
        var source = Sample("photo.jpg", orientation: 6);
        var parameter = new DrawImage.Parameter { TransferMetadata = new List<string> { "System.Photo.*" } };
        var output = Convert(source, PathOf("photo.tif"), parameter);
        var frame = DrawImageSample.Load(output)[0];
        using (Assert.EnterMultipleScope()) {
            Assert.That((frame.PixelWidth, frame.PixelHeight), Is.EqualTo((80, 120)));
            Assert.That(DrawImageSample.RedCorner(frame), Is.EqualTo("TopRight"));
            Assert.That(DrawImageSample.Orientation(frame), Is.Null.Or.EqualTo((ushort)1));
        }
    }

    [Test]
    public void Save_RotatedJpegWithCopiedMetadata_WritesNormalOrientation() {
        var source = Sample("photo.jpg", orientation: 6);
        var parameter = new DrawImage.Parameter { Rotate = 90, CopyMetadata = true };
        var output = Convert(source, PathOf("photo-rotated.jpg"), parameter);
        var frame = DrawImageSample.Load(output)[0];
        using (Assert.EnterMultipleScope()) {
            Assert.That((frame.PixelWidth, frame.PixelHeight), Is.EqualTo((120, 80)));
            Assert.That(DrawImageSample.RedCorner(frame), Is.EqualTo("BottomRight"));
            Assert.That(DrawImageSample.Orientation(frame), Is.EqualTo((ushort)1));
        }
    }

    [Test]
    public void Save_RotatedJpegWithoutCopiedMetadata_AppliesOrientation() {
        var source = Sample("photo.jpg", orientation: 6);
        var output = Convert(source, PathOf("photo-small.jpg"), new DrawImage.Parameter());
        var frame = DrawImageSample.Load(output)[0];
        using (Assert.EnterMultipleScope()) {
            Assert.That((frame.PixelWidth, frame.PixelHeight), Is.EqualTo((80, 120)));
            Assert.That(DrawImageSample.RedCorner(frame), Is.EqualTo("TopRight"));
            Assert.That(DrawImageSample.Orientation(frame), Is.Null.Or.EqualTo((ushort)1));
        }
    }

    [TestCase((ushort)1, 120, 80, "TopLeft")]
    [TestCase((ushort)2, 120, 80, "TopRight")]
    [TestCase((ushort)3, 120, 80, "BottomRight")]
    [TestCase((ushort)4, 120, 80, "BottomLeft")]
    [TestCase((ushort)5, 80, 120, "TopLeft")]
    [TestCase((ushort)6, 80, 120, "TopRight")]
    [TestCase((ushort)7, 80, 120, "BottomRight")]
    [TestCase((ushort)8, 80, 120, "BottomLeft")]
    public void Save_OrientedJpegToPng_PreservesDisplayedAppearance(ushort orientation, int width, int height,
                                                                     string corner) {
        var source = Sample("photo.jpg", orientation);
        var frame = DrawImageSample.Load(Convert(source, PathOf("photo.png"), new DrawImage.Parameter()))[0];
        using (Assert.EnterMultipleScope()) {
            Assert.That((frame.PixelWidth, frame.PixelHeight), Is.EqualTo((width, height)));
            Assert.That(DrawImageSample.RedCorner(frame), Is.EqualTo(corner));
        }
    }

    [Test]
    public void Save_RotatedTiffToPng_AppliesOrientation() {
        var source = Sample("scan.tif", orientation: 6);
        var frame = DrawImageSample.Load(Convert(source, PathOf("scan.png"), new DrawImage.Parameter()))[0];
        using (Assert.EnterMultipleScope()) {
            Assert.That((frame.PixelWidth, frame.PixelHeight), Is.EqualTo((80, 120)));
            Assert.That(DrawImageSample.RedCorner(frame), Is.EqualTo("TopRight"));
        }
    }

    [TestCase(90, 60, null, 60, 90)]
    [TestCase(90, 60, 30, 60, 30)]
    [TestCase(270, null, 30, 20, 30)]
    [TestCase(270, 60, 30, 60, 30)]
    [TestCase(180, 60, null, 60, 40)]
    public void Save_RotationWithSize_AppliesSizeToOutput(int rotate, int? width, int? height, int expectedWidth,
                                                          int expectedHeight) {
        var source = Sample("image.png");
        var parameter = new DrawImage.Parameter { Rotate = rotate, Width = width, Height = height };
        var frame = DrawImageSample.Load(Convert(source, PathOf("output.png"), parameter))[0];
        Assert.That((frame.PixelWidth, frame.PixelHeight), Is.EqualTo((expectedWidth, expectedHeight)));
    }

    [Test]
    public void Save_LargeRotation_MatchesReducedRotation() {
        var source = Sample("photo.jpg", orientation: 6);
        var parameter = new DrawImage.Parameter { Rotate = 2147483610, Width = 60 };
        var frame = DrawImageSample.Load(Convert(source, PathOf("photo.png"), parameter))[0];
        using (Assert.EnterMultipleScope()) {
            Assert.That((frame.PixelWidth, frame.PixelHeight), Is.EqualTo((60, 40)));
            Assert.That(DrawImageSample.RedCorner(frame), Is.EqualTo("BottomRight"));
        }
    }

    [Test]
    public void SourceWidth_LargeSource_DoesNotBufferSourceFile() {
        var source = DrawImageSample.CreateNoise(PathOf("noise.png"), 1024, 1024);
        var requests = new[] { new KeyValuePair<FileInfo, string>(source, PathOf("noise-small.png")) };
        var parameter = new DrawImage.Parameter { Width = 16 };
        var draw = Drawing(source, parameter, requests);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var width = draw.SourceWidth;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Save(draw);
        using (Assert.EnterMultipleScope()) {
            Assert.That(width, Is.EqualTo(1024));
            Assert.That(allocated, Is.LessThan(source.Length / 4), $"Source is {source.Length} bytes.");
        }
    }

    [Test]
    public void Save_RotatedJpegWithWidth_AppliesWidthToOutput() {
        var source = Sample("photo.jpg", orientation: 6);
        var parameter = new DrawImage.Parameter { Width = 40 };
        var frame = DrawImageSample.Load(Convert(source, PathOf("photo.png"), parameter))[0];
        Assert.That((frame.PixelWidth, frame.PixelHeight), Is.EqualTo((40, 60)));
    }
}
