using System.Runtime.InteropServices;
using HANDLE = System.IntPtr;
using HGDIOBJ = System.IntPtr;
using LPVOID = System.IntPtr;

namespace Domore.Runtime.Win32 {
    public static class gdi32 {
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] HGDIOBJ hObject);

        [DllImport("gdi32.dll")]
        public static extern int GetObject([In] HANDLE h, [In] int c, [Out] LPVOID pv);
    }
}
