using System;
using System.Runtime.InteropServices;

namespace Brows.Runtime.Win32; 
[StructLayout(LayoutKind.Sequential)]
struct PREVIEWHANDLERFRAMEINFO {
    public IntPtr haccel;
    public uint cAccelEntries;
}
