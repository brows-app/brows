using System.Runtime.InteropServices;

namespace Brows.Runtime.Win32; 
[StructLayout(LayoutKind.Sequential)]
public struct ACCEL {
    public byte fVirt;
    public ushort key;
    public ushort cmd;
}
