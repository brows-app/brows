using Domore.Runtime.Win32;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Exports;

    internal sealed class IconDriveInfo : IIconDriveInfo {
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

        public async Task<object> Icon(DriveInfo driveInfo, CancellationToken cancellationToken) {
            if (driveInfo == null) {
                return null;
            }
            var stock = Stock(driveInfo.DriveType);
            var icon = await Win32Icon.Load(stock, cancellationToken);
            return icon;
        }
    }
}
