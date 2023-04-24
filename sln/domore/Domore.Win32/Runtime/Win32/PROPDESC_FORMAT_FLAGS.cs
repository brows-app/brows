using System;

namespace Domore.Runtime.Win32 {
    [Flags]
    public enum PROPDESC_FORMAT_FLAGS : uint {
        DEFAULT = 0,
        PREFIXNAME = 0x1,
        FILENAME = 0x2,
        ALWAYSKB = 0x4,
        RESERVED_RIGHTTOLEFT = 0x8,
        SHORTTIME = 0x10,
        LONGTIME = 0x20,
        HIDETIME = 0x40,
        SHORTDATE = 0x80,
        LONGDATE = 0x100,
        HIDEDATE = 0x200,
        RELATIVEDATE = 0x400,
        USEEDITINVITATION = 0x800,
        READONLY = 0x1000,
        NOAUTOREADINGORDER = 0x2000
    }
}
