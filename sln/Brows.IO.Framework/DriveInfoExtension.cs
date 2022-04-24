using System;
using System.IO;

namespace Brows {
    using Gui;

    public static class DriveInfoExtension {
        public static IconStock GetIconStock(this DriveInfo driveInfo) {
            if (null == driveInfo) throw new ArgumentNullException(nameof(driveInfo));
            switch (driveInfo.DriveType) {
                case DriveType.CDRom: return IconStock.DriveCD;
                case DriveType.Fixed: return IconStock.DriveFixed;
                case DriveType.Network: return IconStock.DriveNetwork;
                case DriveType.Ram: return IconStock.DriveRam;
                case DriveType.Removable: return IconStock.DriveRemovable;
            }
            return IconStock.DriveUnknown;
        }
    }
}
