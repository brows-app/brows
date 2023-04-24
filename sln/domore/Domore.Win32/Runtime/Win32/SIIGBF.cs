using System;

namespace Domore.Runtime.Win32 {
    [Flags]
    public enum SIIGBF : uint {
        RESIZETOFIT = 0x00,
        BIGGERSIZEOK = 0x01,
        MEMORYONLY = 0x02,
        ICONONLY = 0x00000004,
        THUMBNAILONLY = 0x00000008,
        INCACHEONLY = 0x00000010,
        CROPTOSQUARE = 0x00000020,
        WIDETHUMBNAILS = 0x00000040,
        ICONBACKGROUND = 0x00000080,
        SCALEUP = 0x00000100
    }
}
