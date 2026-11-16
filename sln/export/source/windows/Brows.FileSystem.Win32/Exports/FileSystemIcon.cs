using Brows.Win32;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Exports;

internal sealed class FileSystemIcon : IFileSystemIcon {
    [ImportRequired]
    internal IWin32WindowsServices Services { get; set; }

    public async Task<bool> Work(FileSystemInfo info, IFileSystemIconHint hint, Action<object> set, CancellationToken token) {
        if (set == null) return false;
        if (info == null) return false;
        var result = default(object);
        if (info is DirectoryInfo) {
            var icon =
                hint?.DirectoryOpen == true ? Win32StockIcon.FolderOpen :
                hint?.DirectoryOpen == false ? Win32StockIcon.FolderBack :
                Win32StockIcon.Folder;
            result = await Services.GetIconSource(icon, token);
        }
        else {
            result = await Services.GetIconSource(info.FullName, info.Attributes, token);
        }
        if (result == null) {
            return false;
        }
        set(result);
        return true;
    }
}
