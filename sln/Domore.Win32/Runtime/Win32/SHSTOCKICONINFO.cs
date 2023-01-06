using System;
using System.Runtime.InteropServices;

namespace Domore.Runtime.Win32 {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHSTOCKICONINFO {
        public uint cbSize;
        public IntPtr hIcon;
        public int iSysImageIndex;
        public int iIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = shell32.MAX_PATH)]
        public string szPath;

        public static uint Size =>
            _Size == 0 ? (
            _Size = (uint)Marshal.SizeOf<SHSTOCKICONINFO>()) :
            _Size;
        private static uint _Size;
    }
}
