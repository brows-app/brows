using Brows.Composition;
using Brows.Win32;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Brows;

[TestFixture]
internal sealed class FileSystemImageSourceTest {
    private static Task<BitmapSource> Get(IWin32WindowsServices services, FileSystemInfo info) {
        IFileSystemImageSource source = new FileSystemImageSource { Services = services };
        return source.Get(info, 100, 100, CancellationToken.None);
    }

    private static void SetupNotAvailable(Mock<IWin32WindowsServices> services, string path) {
        services
            .Setup(s => s.GetThumbnailSource(path, 100, 100, true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Win32ThumbnailException("No thumbnail.", new COMException()));
    }

    [Test]
    public async Task Get_WhenShellProvidesThumbnail_ReturnsIt() {
        using var directory = new TemporaryImageDirectory();
        var file = directory.CreatePng("image.png", width: 200, height: 100);
        var thumbnail = TestShellImage.Create();
        var services = new Mock<IWin32WindowsServices>();
        services
            .Setup(s => s.GetThumbnailSource(file.FullName, 100, 100, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thumbnail);

        var image = await Get(services.Object, file);

        Assert.That(image, Is.SameAs(thumbnail));
        services.Verify(s => s.GetThumbnailSource(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), false, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Get_WhenThumbnailIsNotAvailable_DecodesImageFromStream() {
        using var directory = new TemporaryImageDirectory();
        var file = directory.CreatePng("image.png", width: 200, height: 100);
        var services = new Mock<IWin32WindowsServices>();
        SetupNotAvailable(services, file.FullName);

        var image = await Get(services.Object, file);

        Assert.That(image, Is.Not.Null);
        using (Assert.EnterMultipleScope()) {
            Assert.That(image.PixelWidth, Is.EqualTo(100));
            Assert.That(image.PixelHeight, Is.EqualTo(50));
        }
        services.Verify(s => s.GetThumbnailSource(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), false, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Get_WhenThumbnailIsNotAvailableAndFileIsNotAnImage_ReturnsShellImage() {
        using var directory = new TemporaryImageDirectory();
        var file = directory.CreateText("notes.txt");
        var icon = TestShellImage.Create();
        var services = new Mock<IWin32WindowsServices>();
        SetupNotAvailable(services, file.FullName);
        services
            .Setup(s => s.GetThumbnailSource(file.FullName, 100, 100, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(icon);

        var image = await Get(services.Object, file);

        Assert.That(image, Is.SameAs(icon));
    }

    [Test]
    public async Task Get_WhenInfoIsDirectory_ReturnsShellImageWithoutThrowing() {
        using var directory = new TemporaryImageDirectory();
        var info = new DirectoryInfo(directory.Path);
        var icon = TestShellImage.Create();
        var services = new Mock<IWin32WindowsServices>();
        services
            .Setup(s => s.GetThumbnailSource(info.FullName, 100, 100, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(icon);

        var image = await Get(services.Object, info);

        Assert.That(image, Is.SameAs(icon));
        services.Verify(s => s.GetThumbnailSource(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), true, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Get_WhenInfoIsNull_ThrowsArgumentNullException() {
        var services = new Mock<IWin32WindowsServices>();

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () => await Get(services.Object, null));

        Assert.That(exception.ParamName, Is.EqualTo("info"));
    }

    [Test]
    public void Get_WhenServicesAreNotImported_ThrowsInvalidOperationException() {
        using var directory = new TemporaryImageDirectory();
        var file = directory.CreatePng("image.png", width: 200, height: 100);

        Assert.That(async () => await Get(null, file), Throws.InstanceOf<InvalidOperationException>());
    }
}
