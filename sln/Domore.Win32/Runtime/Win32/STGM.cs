using System;
namespace Domore.Runtime.Win32 {
    [Flags]
    public enum STGM : uint {
        READ = 0x00000000,
        WRITE = 0x00000001,
        READWRITE = 0x00000002,
        SHARE_DENY_NONE = 0x00000040,
        SHARE_DENY_READ = 0x00000030,
        SHARE_DENY_WRITE = 0x00000020,
        SHARE_EXCLUSIVE = 0x00000010,
        PRIORITY = 0x00040000,
        CREATE = 0x00001000,
        CONVERT = 0x0002000,
        FAILIFTHERE = 0x0000000,
        DIRECT = 0x00000000,
        TRANSACTED = 0x00010000,
        NOSCRATCH = 0x00100000,
        NOSNAPSHOT = 0x00200000,
        SIMPLE = 0x08000000,
        DIRECT_SWMR = 0x00400000,
        DELETEONRELEASE = 0x04000000
    }
}
