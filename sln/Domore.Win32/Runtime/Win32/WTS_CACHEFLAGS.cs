using System;

namespace Domore.Runtime.Win32 {
    [Flags]
    public enum WTS_CACHEFLAGS : uint {
        DEFAULT = 0x00000000,
        LOWQUALITY = 0x00000001,
        CACHED = 0x00000002
    }
}
