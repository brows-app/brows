using System;

namespace Domore.Runtime.Win32 {
    [Flags]
    public enum GETPROPERTYSTOREFLAGS {
        DEFAULT = 0,
        HANDLERPROPERTIESONLY = 0x1,
        READWRITE = 0x2,
        TEMPORARY = 0x4,
        FASTPROPERTIESONLY = 0x8,
        OPENSLOWITEM = 0x10,
        DELAYCREATION = 0x20,
        BESTEFFORT = 0x40,
        NO_OPLOCK = 0x80,
        PREFERQUERYPROPERTIES = 0x100,
        EXTRINSICPROPERTIES = 0x200,
        EXTRINSICPROPERTIESONLY = 0x400,
        VOLATILEPROPERTIES = 0x800,
        VOLATILEPROPERTIESONLY = 0x1000,
        MASK_VALID = 0x1fff
    }
}
