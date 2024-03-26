using System;

using DWORD = System.UInt32;

namespace Domore.Runtime.Win32 {
    [Flags]
    public enum DEVICE_NOTIFY : DWORD {
        WINDOW_HANDLE = 0x00000000,
        SERVICE_HANDLE = 0x00000001,
        ALL_INTERFACE_CLASSES = 0x00000004
    }
}
