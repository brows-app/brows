using Brows.Win32;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Exports;

internal sealed class OverlayFileSystemInfo : IOverlayFileSystemInfo {
    [ImportRequired]
    internal IWin32WindowsServices Services { get; set; }

    public async Task<bool> Work(FileSystemInfo fileSystemInfo, Action<object> set, CancellationToken token) {
        if (fileSystemInfo == null) {
            return false;
        }
        if (set == null) {
            return false;
        }
        bool ret(object obj) {
            set(obj);
            return true;
        }
        var result = await Services.GetOverlayIconSource(fileSystemInfo.FullName,
                                                         fileSystemInfo.Attributes,
                                                         token);
        if (result != null) {
            return ret(result);
        }
        if (fileSystemInfo is DirectoryInfo directory) {
            if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
                return ret(await Services.GetIconSource(Win32StockIcon.Link, token));
            }
        }
        return false;
    }
}
