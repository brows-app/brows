using System;
using System.Runtime.InteropServices;
using BOOL = System.Int32;
using DWORD = System.UInt32;
using HACCEL = System.IntPtr;
using HANDLE = System.IntPtr;
using HDEVNOTIFY = System.IntPtr;
using HWND = System.IntPtr;
using LPARAM = System.Int64;
using LPVOID = System.IntPtr;
using LRESULT = System.Int64;
using UINT = System.UInt32;
using WPARAM = System.UInt32;

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
        public static extern LRESULT DefWindowProcW([In] HWND hWnd, [In] UINT Msg, [In] WPARAM wParam, [In] LPARAM lParam);

        [DllImport("user32.dll", SetLastError = true, PreserveSig = true)]
        public static extern ushort RegisterClassExW([In] ref WNDCLASSEXW unnamedParam1);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true)]
        public static extern HACCEL CreateAcceleratorTableW([In, MarshalAs(UnmanagedType.LPArray)] ACCEL[] paccel, [In] int cAccel);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true)]
        public static extern HDEVNOTIFY RegisterDeviceNotificationW([In] HANDLE hRecipient, [In] LPVOID NotificationFilter, [In] DEVICE_NOTIFY Flags);

        [DllImport("user32.dll", PreserveSig = true, SetLastError = true)]
        public static extern BOOL UnregisterDeviceNotification([In] HDEVNOTIFY Handle);
    }
}
