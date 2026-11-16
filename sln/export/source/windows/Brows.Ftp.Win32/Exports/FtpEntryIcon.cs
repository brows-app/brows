using Brows.DirectoryListing;
using Brows.Win32;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Brows.Exports;

internal sealed class FtpEntryIcon : IFtpEntryIcon {
    [ImportRequired]
    internal IWin32WindowsServices Services { get; set; }

    public async Task<bool> Work(DirectoryListItem info, Action<object> set, CancellationToken token) {
        if (null == info) return false;
        if (null == set) return false;
        var task = default(Task<BitmapSource>);
        var flags = info.Flags;
        if (flags.HasFlag(DirectoryListItemFlags.Directory)) {
            task = Services.GetIconSource(Win32StockIcon.Folder, token);
        }
        else {
            task = Services.GetIconSource(Path.GetExtension(info.Name), FileAttributes.None, token);
        }
        if (task == null) {
            return false;
        }
        set(await task);
        return true;
    }
}
