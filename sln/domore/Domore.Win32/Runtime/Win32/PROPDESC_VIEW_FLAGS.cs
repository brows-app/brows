using System;

namespace Domore.Runtime.Win32 {
    [Flags]
    public enum PROPDESC_VIEW_FLAGS : uint {
        DEFAULT = 0,
        CENTERALIGN = 0x1,
        RIGHTALIGN = 0x2,
        BEGINNEWGROUP = 0x4,
        FILLAREA = 0x8,
        SORTDESCENDING = 0x10,
        SHOWONLYIFPRESENT = 0x20,
        SHOWBYDEFAULT = 0x40,
        SHOWINPRIMARYLIST = 0x80,
        SHOWINSECONDARYLIST = 0x100,
        HIDELABEL = 0x200,
        HIDDEN = 0x800,
        CANWRAP = 0x1000,
        MASK_ALL = 0x1bff
    }
}
