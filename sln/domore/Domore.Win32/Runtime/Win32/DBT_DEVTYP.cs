using DWORD = System.UInt32;

namespace Domore.Runtime.Win32 {
    public enum DBT_DEVTYP : DWORD {
        DEVICEINTERFACE = 0x00000005,
        HANDLE = 0x00000006,
        OEM = 0x00000000,
        PORT = 0x00000003,
        VOLUME = 0x00000002
    }
}
