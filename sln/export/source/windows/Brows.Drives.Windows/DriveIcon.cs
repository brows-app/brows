using Brows.Win32;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Brows;

internal sealed class DriveIcon : IDriveIcon {
    private Win32StockIcon Stock(DriveType kind) {
        return kind switch {
            DriveType.CDRom => Win32StockIcon.DriveCD,
            DriveType.Fixed => Win32StockIcon.DriveFixed,
            DriveType.Network => Win32StockIcon.DriveNet,
            DriveType.NoRootDirectory => Win32StockIcon.DriveNetDisabled,
            DriveType.Ram => Win32StockIcon.DriveRam,
            DriveType.Removable => Win32StockIcon.DriveRemove,
            _ => Win32StockIcon.DriveUnknown,
        };
    }

    [ImportRequired]
    internal IWin32WindowsServices Services { get; set; }

    public async Task<bool> Work(DriveInfo info, Action<object> set, CancellationToken cancellationToken) {
        if (set is null) return false;
        if (info is null) return false;
        var stock = Stock(info.DriveType);
        var icon = await Services.GetIconSource(stock, cancellationToken);
        set(icon);
        return true;
    }
}
