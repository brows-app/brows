using System.Runtime.InteropServices;

namespace Brows.Runtime.Win32 {
    [StructLayout(LayoutKind.Sequential)]
    internal struct SIZE {
        public int cx;
        public int cy;
    }
}
