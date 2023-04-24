using System.Runtime.InteropServices;

namespace Domore.Runtime.Win32 {
    [StructLayout(LayoutKind.Sequential)]
    public struct FILE_CASE_SENSITIVE_INFORMATION {
        [MarshalAs(UnmanagedType.U4)]
        public uint Flags;
    }
}
