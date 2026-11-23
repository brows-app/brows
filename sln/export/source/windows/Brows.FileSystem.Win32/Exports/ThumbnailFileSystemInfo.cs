using Brows.Win32;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Exports;

internal sealed class ThumbnailFileSystemInfo : IThumbnailFileSystemInfo {
    [ImportRequired]
    internal IWin32WindowsServices Services { get; set; }

    public async Task<bool> Work(FileSystemInfo fileSystemInfo,
                                 int width,
                                 int height,
                                 Action<object> set,
                                 CancellationToken token) {
        if (set is null) {
            return false;
        }
        if (fileSystemInfo is null) {
            return false;
        }
        var services = Services ?? throw new InvalidOperationException();
        var path = fileSystemInfo.FullName;
        var result = await services.GetThumbnailSource(path, width, height, throwIfNotAvailable: false, token);
        if (result is null) {
            return false;
        }
        set(result);
        return true;
    }
}
