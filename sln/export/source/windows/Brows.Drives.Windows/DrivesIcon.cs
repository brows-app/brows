using Brows.Win32;
using System;
using System.Threading.Tasks;

namespace Brows;

internal sealed class DrivesIcon : IDrivesIcon {
    [ImportRequired]
    internal IWin32WindowsServices Services { get; set; }

    public async Task<bool> Work(IDrives drives, Action<object> set, CancellationToken token) {
        if (set == null) {
            return false;
        }
        var icon = await Services.GetIconSource(Win32StockIcon.DesktopPC, token);
        set(icon);
        return true;
    }
}
