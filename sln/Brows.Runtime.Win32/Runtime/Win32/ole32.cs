using System;
using System.Runtime.InteropServices;

namespace Brows.Runtime.Win32 {
    internal class ole32 {
        [DllImport("ole32.dll")]
        public static extern void CoCreateInstance(
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
            [MarshalAs(UnmanagedType.IUnknown)] object pUnkOuter,
            CLSCTX dwClsContext,
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [Out] out IntPtr ppv);
    }
}
