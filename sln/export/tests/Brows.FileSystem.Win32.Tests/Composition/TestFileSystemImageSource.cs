using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Brows.Composition;

/*
 * IFileSystemImageSource is internal, so Moq cannot create a proxy for it.
 * This fake records each request and returns a configured image.
 */
internal sealed class TestFileSystemImageSource : IFileSystemImageSource {
    public BitmapSource Image { get; init; }
    public List<(FileSystemInfo Info, int Width, int Height)> Requests { get; } = [];

    Task<BitmapSource> IFileSystemImageSource.Get(FileSystemInfo info,
                                                  int width,
                                                  int height,
                                                  CancellationToken token) {
        Requests.Add((info, width, height));
        return Task.FromResult(Image);
    }
}
