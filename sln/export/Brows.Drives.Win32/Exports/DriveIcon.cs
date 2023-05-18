using Brows.Exports;
using Domore.Runtime.Win32;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class DriveIcon : IDriveIcon {
        private SHSTOCKICONID Stock(DriveType kind) {
            switch (kind) {
                case DriveType.CDRom:
                    return SHSTOCKICONID.DRIVECD;
                case DriveType.Fixed:
                    return SHSTOCKICONID.DRIVEFIXED;
                case DriveType.Network:
                    return SHSTOCKICONID.DRIVENET;
                case DriveType.NoRootDirectory:
                    return SHSTOCKICONID.DRIVENETDISABLED;
                case DriveType.Ram:
                    return SHSTOCKICONID.DRIVERAM;
                case DriveType.Removable:
                    return SHSTOCKICONID.DRIVEREMOVE;
                default:
                    return SHSTOCKICONID.DRIVEUNKNOWN;
            }
        }

        public async Task<bool> Work(DriveInfo info, Action<object> set, CancellationToken cancellationToken) {
            if (set == null) return false;
            if (info == null) return false;
            var stock = Stock(info.DriveType);
            var icon = await Win32Icon.Load(stock, cancellationToken);
            set(icon);
            return true;
        }
    }
}
