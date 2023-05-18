using System;
using System.Runtime.InteropServices;

namespace Domore.Runtime.Win32 {
    public static class oleaut32 {
        [DllImport("oleaut32.dll", PreserveSig = true)]
        public static extern void VariantInit([Out] IntPtr pvarg);

        [DllImport("oleaut32.dll", PreserveSig = true)]
        public static extern HRESULT VariantClear([In, Out] IntPtr pvarg);
    }
}
