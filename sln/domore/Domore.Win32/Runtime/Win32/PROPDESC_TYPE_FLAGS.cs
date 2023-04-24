using System;

namespace Domore.Runtime.Win32 {
    [Flags]
    public enum PROPDESC_TYPE_FLAGS : uint {
        DEFAULT = 0,
        MULTIPLEVALUES = 0x1,
        ISINNATE = 0x2,
        ISGROUP = 0x4,
        CANGROUPBY = 0x8,
        CANSTACKBY = 0x10,
        ISTREEPROPERTY = 0x20,
        INCLUDEINFULLTEXTQUERY = 0x40,
        ISVIEWABLE = 0x80,
        ISQUERYABLE = 0x100,
        CANBEPURGED = 0x200,
        SEARCHRAWVALUE = 0x400,
        DONTCOERCEEMPTYSTRINGS = 0x800,
        ALWAYSINSUPPLEMENTALSTORE = 0x1000,
        ISSYSTEMPROPERTY = 0x80000000,
        MASK_ALL = 0x80001fff
    }
}
