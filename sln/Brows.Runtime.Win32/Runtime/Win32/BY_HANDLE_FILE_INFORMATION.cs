using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Brows.Runtime.Win32 {
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct BY_HANDLE_FILE_INFORMATION {
        public uint FileAttributes;
        public FILETIME CreationTime;
        public FILETIME LastAccessTime;
        public FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }
}
