using Brows.Composition;
using Brows.Win32;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Brows.Exports;

[TestFixture]
internal sealed class ThumbnailFileSystemInfoTest {
    private static FileInfo ImageFile =>
        new(Path.Combine(Path.GetTempPath(), "image.png"));

    [Test]
    public async Task Work_SetsShellImageWithoutRequiringThumbnail() {
        var expected = TestShellImage.Create();
        var info = ImageFile;
        var services = new Mock<IWin32WindowsServices>();
        services
            .Setup(s => s.GetThumbnailSource(info.FullName, 100, 100, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        IThumbnailFileSystemInfo subject = new ThumbnailFileSystemInfo { Services = services.Object };
        var actual = default(object);

        var worked = await subject.Work(info, 100, 100, set: image => actual = image, CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(worked, Is.True);
            Assert.That(actual, Is.SameAs(expected));
        }
        services.Verify(s => s.GetThumbnailSource(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), true, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Work_WhenShellReturnsNull_ReturnsFalse() {
        var services = new Mock<IWin32WindowsServices>();
        services
            .Setup(s => s.GetThumbnailSource(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BitmapSource)null);
        IThumbnailFileSystemInfo subject = new ThumbnailFileSystemInfo { Services = services.Object };
        var set = false;

        var worked = await subject.Work(ImageFile, 100, 100, set: _ => set = true, CancellationToken.None);

        using (Assert.EnterMultipleScope()) {
            Assert.That(worked, Is.False);
            Assert.That(set, Is.False);
        }
    }

    [Test]
    public void Work_WhenServicesAreNotImported_ThrowsInvalidOperationException() {
        IThumbnailFileSystemInfo subject = new ThumbnailFileSystemInfo();

        Assert.That(async () => await subject.Work(ImageFile, 100, 100, set: _ => { }, CancellationToken.None),
            Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public async Task Work_WhenComposed_GetsImageThroughImportedServices() {
        var thumbnail = TestShellImage.Create();
        var services = new Mock<IWin32WindowsServices>();
        services
            .Setup(s => s.GetThumbnailSource(It.IsAny<string>(), 100, 100, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thumbnail);
        var subject = new ThumbnailFileSystemInfo();
        var actual = default(object);

        await ImportSandbox.Enter([subject, services.Object], async () => {
            await ((IThumbnailFileSystemInfo)subject).Work(ImageFile, 100, 100, set: image => actual = image,
                CancellationToken.None);
        });

        Assert.That(actual, Is.SameAs(thumbnail));
    }
}
