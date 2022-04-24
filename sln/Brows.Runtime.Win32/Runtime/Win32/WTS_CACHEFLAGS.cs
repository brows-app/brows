using System;

namespace Brows.Runtime.Win32 {
    [Flags]
    internal enum WTS_CACHEFLAGS : uint {
        DEFAULT = 0x00000000,
        LOWQUALITY = 0x00000001,
        CACHED = 0x00000002
    }
}
