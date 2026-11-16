using Brows.Win32;
using System;
using System.Threading.Tasks;

namespace Brows.Exports;

internal sealed class SftpProviderIcon : ISftpProviderIcon {
    [ImportRequired]
    internal IWin32WindowsServices Services { get; set; }

    public async Task<bool> Work(Action<object> set, CancellationToken token) {
        if (set is null) {
            return false;
        }
        set(await Services.GetIconSource(Win32StockIcon.Folder, token));
        return true;
    }
}
