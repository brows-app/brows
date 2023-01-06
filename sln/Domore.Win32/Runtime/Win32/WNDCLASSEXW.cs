using System;
using System.Runtime.InteropServices;

namespace Domore.Runtime.Win32 {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WNDCLASSEXW {
        [MarshalAs(UnmanagedType.U4)]
        public int cbSize;
        [MarshalAs(UnmanagedType.U4)]
        public int style;
        public IntPtr lpfnWndProc; // not WndProc
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;

        //Use this function to make a new one with cbSize already filled in.
        //For example:
        //var WndClss = WNDCLASSEX.Build()
        public static WNDCLASSEXW Build() {
            var nw = new WNDCLASSEXW();
            nw.cbSize = Marshal.SizeOf(typeof(WNDCLASSEXW));
            return nw;
        }
    }
}
