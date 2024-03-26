using System.Runtime.InteropServices;

using DWORD = System.UInt32;

namespace Domore.Runtime.Win32 {
    [StructLayout(LayoutKind.Sequential)]
    public struct DEV_BROADCAST_HDR {
        [MarshalAs(UnmanagedType.U4)]
        public DWORD dbch_size;

        [MarshalAs(UnmanagedType.U4)]
        public DBT_DEVTYP dbch_devicetype;

        [MarshalAs(UnmanagedType.U4)]
        public DWORD dbch_reserved;
    }
}
