using Brows.Win32;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Exports;

internal sealed class SftpEntryIcon : ISftpEntryIcon {
    [ImportRequired]
    internal IWin32WindowsServices Services { get; set; }

    public async Task<bool> Work(FileProtocolEntryKind kind, string extension, Action<object> set, CancellationToken token) {
        if (set is null) {
            return false;
        }
        var task = kind is FileProtocolEntryKind.Directory or FileProtocolEntryKind.DirectoryLink
            ? Services.GetIconSource(Win32StockIcon.Folder, token)
            : Services.GetIconSource(extension, FileAttributes.None, token);
        set(await task);
        return true;
    }
}
