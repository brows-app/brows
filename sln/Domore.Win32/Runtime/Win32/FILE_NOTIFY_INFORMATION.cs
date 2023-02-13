using System.Runtime.InteropServices;

namespace Domore.Runtime.Win32 {
    [StructLayout(LayoutKind.Sequential)]
    public struct FILE_NOTIFY_INFORMATION {
        public uint NextEntryOffset;
        public uint Action;
        public uint FileNameLength;
        public ushort[] FileName;
    }
}
