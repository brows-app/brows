using System;
using System.Runtime.InteropServices;

using DWORD = System.UInt32;
using wchar_t = short;

namespace Domore.Runtime.Win32 {
    [StructLayout(LayoutKind.Sequential)]
    public struct DEV_BROADCAST_DEVICEINTERFACE_W {
        public DWORD dbcc_size;
        public DWORD dbcc_devicetype;
        public DWORD dbcc_reserved;
        public Guid dbcc_classguid;
        public wchar_t dbcc_name;
    }
}
