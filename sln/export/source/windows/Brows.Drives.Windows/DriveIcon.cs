using Brows.Runtime.Win32;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Brows;

internal sealed class DriveIcon : IDriveIcon {
    private SHSTOCKICONID Stock(DriveType kind) {
        return kind switch {
            DriveType.CDRom => SHSTOCKICONID.DRIVECD,
            DriveType.Fixed => SHSTOCKICONID.DRIVEFIXED,
            DriveType.Network => SHSTOCKICONID.DRIVENET,
            DriveType.NoRootDirectory => SHSTOCKICONID.DRIVENETDISABLED,
            DriveType.Ram => SHSTOCKICONID.DRIVERAM,
            DriveType.Removable => SHSTOCKICONID.DRIVEREMOVE,
            _ => SHSTOCKICONID.DRIVEUNKNOWN,
        };
    }

    public async Task<bool> Work(DriveInfo info, Action<object> set, CancellationToken cancellationToken) {
        if (set is null) return false;
        if (info is null) return false;
        var stock = Stock(info.DriveType);
        var icon = await Win32Icon.Load(stock, cancellationToken);
        set(icon);
        return true;
    }
}
