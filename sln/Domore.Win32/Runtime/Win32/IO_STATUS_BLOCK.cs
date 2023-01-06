using System.Runtime.InteropServices;

namespace Domore.Runtime.Win32 {
    [StructLayout(LayoutKind.Sequential)]
    public struct IO_STATUS_BLOCK {
        [MarshalAs(UnmanagedType.U4)]
        public NTSTATUS Status;
        public ulong Information;
    }
}
