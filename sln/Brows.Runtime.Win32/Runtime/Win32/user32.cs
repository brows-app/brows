using System;
using System.Runtime.InteropServices;

namespace Brows.Runtime.Win32 {
    internal static class user32 {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);
    }
}
