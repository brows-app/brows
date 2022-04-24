using System;
using System.Runtime.InteropServices;

namespace Brows.Runtime.Win32 {
    [StructLayout(LayoutKind.Sequential)]
    public struct MULTI_QI {
        [MarshalAs(UnmanagedType.LPStruct)] public Guid pIID;
        [MarshalAs(UnmanagedType.Interface)] public object pItf;
        public int hr;
    }
}
