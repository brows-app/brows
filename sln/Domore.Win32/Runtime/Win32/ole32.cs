using System;
using System.Runtime.InteropServices;

namespace Domore.Runtime.Win32 {
    public class ole32 {
        [DllImport("ole32.dll", PreserveSig = true)]
        public static extern HRESULT CoCreateInstance(
            [In] ref Guid rclsid,
            IntPtr pUnkOuter,
            CLSCTX dwClsContext,
            [In] ref Guid riid,
            [Out] out IntPtr ppv);
    }
}
