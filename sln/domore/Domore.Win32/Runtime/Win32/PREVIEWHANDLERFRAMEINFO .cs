using System;
using System.Runtime.InteropServices;

namespace Domore.Runtime.Win32 {
    [StructLayout(LayoutKind.Sequential)]
    struct PREVIEWHANDLERFRAMEINFO {
        public IntPtr haccel;
        public uint cAccelEntries;
    }
}
