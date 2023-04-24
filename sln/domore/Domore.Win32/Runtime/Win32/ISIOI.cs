using System;

namespace Domore.Runtime.Win32 {
    [Flags]
    public enum ISIOI : uint {
        ICONFILE = 0x00000001,
        ICONINDEX = 0x00000002
    }
}
