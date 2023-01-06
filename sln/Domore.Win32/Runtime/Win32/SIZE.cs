using System.Runtime.InteropServices;

namespace Domore.Runtime.Win32 {
    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE {
        public int cx;
        public int cy;
    }
}
