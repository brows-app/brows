using System.Runtime.InteropServices;

using DWORD = System.UInt32;

namespace Domore.Runtime.Win32 {
    [StructLayout(LayoutKind.Sequential)]
    public struct DEV_BROADCAST_VOLUME {
        [MarshalAs(UnmanagedType.U4)]
        public DWORD dbcv_size;

        [MarshalAs(UnmanagedType.U4)]
        public DBT_DEVTYP dbcv_devicetype;

        [MarshalAs(UnmanagedType.U4)]
        public DWORD dbcv_reserved;

        [MarshalAs(UnmanagedType.U4)]
        public DWORD dbcv_unitmask;

        [MarshalAs(UnmanagedType.U2)]
        public DBTF dbcv_flags;
    }
}
