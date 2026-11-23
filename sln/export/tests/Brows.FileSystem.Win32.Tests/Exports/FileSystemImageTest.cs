using Brows.Composition;
using Brows.Win32;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports;

[TestFixture]
internal sealed class FileSystemImageTest {
    private static FileInfo ImageFile =>
        new(Path.Combine(Path.GetTempPath(), "image.png"));

    [Test]
    public async Task Work_SetsImageFromImageSource() {
        var expected = TestShellImage.Create();
        var source = new TestFileSystemImageSource { Image = expected };
        IFileSystemImage subject = new FileSystemImage { ImageSource = source };
        var info = ImageFile;
        var actual = default(object);

        var worked = await subject.Work(info, 1280, 960, set: image => actual = image, CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(worked, Is.True);
            Assert.That(actual, Is.SameAs(expected));
            Assert.That(source.Requests, Is.EqualTo(new[] { ((FileSystemInfo)info, 1280, 960) }));
        }
    }

    [Test]
    public async Task Work_WhenImageSourceReturnsNull_ReturnsFalse() {
        var source = new TestFileSystemImageSource { Image = null };
        IFileSystemImage subject = new FileSystemImage { ImageSource = source };
        var set = false;

        var worked = await subject.Work(ImageFile, 1280, 960, set: _ => set = true, CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(worked, Is.False);
            Assert.That(set, Is.False);
        }
    }

    [Test]
    public void Work_WhenImageSourceIsNotImported_ThrowsInvalidOperationException() {
        IFileSystemImage subject = new FileSystemImage();

        Assert.That(async () => await subject.Work(ImageFile, 1280, 960, set: _ => { }, CancellationToken.None),
            Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public async Task Work_WhenComposed_GetsImageThroughImportedServices() {
        var thumbnail = TestShellImage.Create();
        var services = new Mock<IWin32WindowsServices>();
        services
            .Setup(s => s.GetThumbnailSource(It.IsAny<string>(), 1280, 960, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thumbnail);
        var subject = new FileSystemImage();
        var actual = default(object);

        await ImportSandbox.Enter([subject, new FileSystemImageSource(), services.Object], async () => {
            await ((IFileSystemImage)subject).Work(ImageFile, 1280, 960, set: image => actual = image,
                CancellationToken.None);
        });

        Assert.That(actual, Is.SameAs(thumbnail));
    }
}
