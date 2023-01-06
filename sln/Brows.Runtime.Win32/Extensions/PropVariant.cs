using Domore.Runtime.Win32;
using System.Runtime.InteropServices;

namespace Brows.Extensions {
    internal static class PropVariant {
        public static object Value(this PROPVARIANT pv) {
            var p = pv.data1;
            switch (pv.vt) {
                case 0:
                case 1:
                    return null;
                case 16:
                    return (sbyte)p.ToInt32();
                case 17:
                    return (byte)p.ToInt32();
                case 2:
                    return (short)p.ToInt32();
                case 18:
                    return (ushort)p.ToInt32();
                case 3:
                case 22:
                    return p.ToInt32();
                case 19:
                case 23:
                    return unchecked((uint)p.ToInt32());

                case 30:
                    return Marshal.PtrToStringAnsi(p);
                case 31:
                    return Marshal.PtrToStringUni(p);
                default:
                    return null;
            }
        }
    }
}
