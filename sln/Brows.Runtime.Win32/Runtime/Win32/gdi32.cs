using System;
using System.Runtime.InteropServices;

namespace Brows.Runtime.Win32 {
    internal static class gdi32 {
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);
    }
}
