using System;
using System.Runtime.InteropServices;

namespace Domore.Runtime.Win32 {
    [StructLayout(LayoutKind.Sequential)]
    public struct PROPVARIANT {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr data1;
        public IntPtr data2;
        //public IntPtr data3;
        //public IntPtr data4;


        //[FieldOffset(0)]
        //public uint vt;
        //[FieldOffset(4)]
        //public ushort wReserved1;
        //[FieldOffset(6)]
        //public ushort wReserved2;
        //[FieldOffset(8)]
        //public ushort wReserved3;

        //[FieldOffset(10)]
        //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.i)]
        //public fixed byte[] union[32];

        //[FieldOffset(16)]
        //public sbyte cVal;
        //[FieldOffset(16)]
        //public byte bVal;
        //[FieldOffset(16)]
        //public short iVal;
        //[FieldOffset(16)]
        //public ushort uiVal;
        //[FieldOffset(16)]
        //public int lVal;
        //[FieldOffset(16)]
        //public uint ulVal;
        //[FieldOffset(16)]
        //public int intVal;
        //[FieldOffset(16)]
        //public uint uintVal;
        //[FieldOffset(16)]
        //public long hVal;
        //[FieldOffset(16)]
        //public ulong uhVal;
        //[FieldOffset(16)]
        //public float fltVal;
        //[FieldOffset(16)]
        //public double dblVal;
        //[FieldOffset(16)]
        //public bool boolVal;


        //[FieldOffset(16)]
        //[MarshalAs(UnmanagedType.LPStr)]
        //public string pszVal;



        //[FieldOffset(16)]
        //[MarshalAs(UnmanagedType.LPWStr)]
        //public string pwszVal;
    }
}
