using System.Runtime.InteropServices;

namespace Brows.Runtime.Win32 {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }
}
