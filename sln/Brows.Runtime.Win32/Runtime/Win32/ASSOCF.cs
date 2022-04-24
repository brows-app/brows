using System;

namespace Brows.Runtime.Win32 {
    [Flags]
    internal enum ASSOCF {
        NONE = 0x00000000,
        INIT_NOREMAPCLSID = 0x00000001,
        INIT_BYEXENAME = 0x00000002,
        OPEN_BYEXENAME = 0x00000002,
        INIT_DEFAULTTOSTAR = 0x00000004,
        INIT_DEFAULTTOFOLDER = 0x00000008,
        NOUSERSETTINGS = 0x00000010,
        NOTRUNCATE = 0x00000020,
        VERIFY = 0x00000040,
        REMAPRUNDLL = 0x00000080,
        NOFIXUPS = 0x00000100,
        IGNOREBASECLASS = 0x00000200,
        INIT_IGNOREUNKNOWN = 0x00000400,
        INIT_FIXED_PROGID = 0x00000800,
        IS_PROTOCOL = 0x00001000,
        INIT_FOR_FILE = 0x00002000
    }
}
