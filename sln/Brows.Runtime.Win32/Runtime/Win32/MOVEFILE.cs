using System;

namespace Brows.Runtime.Win32 {
    [Flags]
    internal enum MOVEFILE : uint {
        COPY_ALLOWED = 0x2,
        CREATE_HARDLINK = 0x10,
        DELAY_UNTIL_REBOOT = 0x4,
        FAIL_IF_NOT_TRACKABLE = 0x20,
        REPLACE_EXISTING = 0x1,
        WRITE_THROUGH = 0x8
    }
}
