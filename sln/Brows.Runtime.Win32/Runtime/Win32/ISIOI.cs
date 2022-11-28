using System;

namespace Brows.Runtime.Win32 {
    [Flags]
    internal enum ISIOI : uint {
        ICONFILE = 0x00000001,
        ICONINDEX = 0x00000002
    }
}
