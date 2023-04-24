using System;
using System.Runtime.InteropServices;

namespace Domore.Runtime.Win32 {
    public static class gdi32 {
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);
    }
}
