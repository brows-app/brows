using System;
using System.Runtime.InteropServices;

namespace Domore.Runtime.Win32 {
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct PROPERTYKEY {
        public Guid fmtid;
        public uint pid;
    }
}
