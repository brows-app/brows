using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Brows.Composition;

internal interface IFileSystemImageSource : IExport {
    Task<BitmapSource> Get(FileSystemInfo info,
                           int width,
                           int height,
                           CancellationToken token);
}
