using System;
using System.Runtime.InteropServices;

namespace Domore.Runtime.Win32 {
    public static class user32 {
        [DllImport("user32.dll", SetLastError = true, PreserveSig = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", SetLastError = true, PreserveSig = true)]
        public static extern bool GetClientRect([In] IntPtr hWnd, [In, Out] ref RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true, PreserveSig = true)]
        public static extern IntPtr CreateWindowExW(
          [In] uint dwExStyle,
          [In, Optional, MarshalAs(UnmanagedType.LPWStr)] string lpClassName,
          [In, Optional, MarshalAs(UnmanagedType.LPWStr)] string lpWindowName,
          [In] uint dwStyle,
          [In] int X,
          [In] int Y,
          [In] int nWidth,
          [In] int nHeight,
          [In, Optional] IntPtr hWndParent,
          [In, Optional] IntPtr hMenu,
          [In, Optional] IntPtr hInstance,
          [In, Optional] IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true, PreserveSig = true)]
        public static extern bool DestroyWindow([In] IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true, PreserveSig = true)]
        public static extern long DefWindowProcW([In] IntPtr hWnd, [In] uint Msg, [In] uint wParam, [In] long lParam);

        [DllImport("user32.dll", SetLastError = true, PreserveSig = true)]
        public static extern ushort RegisterClassExW([In] ref WNDCLASSEXW unnamedParam1);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true)]
        public static extern IntPtr CreateAcceleratorTableW([In, MarshalAs(UnmanagedType.LPArray)] ACCEL[] paccel, int cAccel);
    }
}
