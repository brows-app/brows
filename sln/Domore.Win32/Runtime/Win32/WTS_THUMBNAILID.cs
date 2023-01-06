using System;
using System.Runtime.InteropServices;

namespace Domore.Runtime.Win32 {
    [StructLayout(LayoutKind.Sequential, Size = 16), Serializable]
    public struct WTS_THUMBNAILID {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        byte[] rgbKey;
    }
}
