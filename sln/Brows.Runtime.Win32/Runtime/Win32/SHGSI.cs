using System;

namespace Brows.Runtime.Win32 {
    [Flags]
    internal enum SHGSI : uint {
        ICONLOCATION = 0,
        ICON = 0x000000100,
        SYSICONINDEX = 0x000004000,
        LINKOVERLAY = 0x000008000,
        SELECTED = 0x000010000,
        LARGEICON = 0x000000000,
        SMALLICON = 0x000000001,
        SHELLICONSIZE = 0x000000004
    }
}
