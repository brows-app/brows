using Brows.Win32;
using Domore.Logs;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Brows;

/*
 * Gets the image of a file-system item. A file's image is the thumbnail the
 * Windows Shell provides. When the Shell cannot provide one, such as for a
 * cloud placeholder file on a network share, the image is decoded from the
 * file's stream. A file that cannot be decoded, and a directory, gets the
 * Shell's icon instead.
 */
internal sealed class FileSystemImageSource : IFileSystemImageSource {
    private static readonly ILog Log = Logging.For(typeof(FileSystemImageSource));

    [ImportRequired]
    internal IWin32WindowsServices Services { get; set; }

    async Task<BitmapSource> IFileSystemImageSource.Get(FileSystemInfo info, int width, int height, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(info);
        var services = Services ?? throw new InvalidOperationException();
        var path = info.FullName;
        if (info is not FileInfo file) {
            return await services.GetThumbnailSource(path, width, height, throwIfNotAvailable: false, token);
        }
        try {
            return await services.GetThumbnailSource(path, width, height, throwIfNotAvailable: true, token);
        }
        catch (Win32ThumbnailException ex) {
            if (Log.Info()) {
                Log.Info($"Decoding the image from a stream, because the Shell has no thumbnail " +
                         $"[0x{ex.HResult:X8}] {path}");
            }
        }
        var decoded = await FileSystemImageDecoder.Decode(file, width, height, token);
        if (decoded is not null) {
            return decoded;
        }
        return await services.GetThumbnailSource(path, width, height, throwIfNotAvailable: false, token);
    }
}
