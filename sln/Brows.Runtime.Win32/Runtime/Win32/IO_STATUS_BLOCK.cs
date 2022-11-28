using System.Runtime.InteropServices;

namespace Brows.Runtime.Win32 {
    [StructLayout(LayoutKind.Sequential)]
    internal struct IO_STATUS_BLOCK {
        [MarshalAs(UnmanagedType.U4)]
        public NTSTATUS Status;
        public ulong Information;
    }
}
