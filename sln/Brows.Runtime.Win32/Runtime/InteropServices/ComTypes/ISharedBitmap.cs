using System;
using System.Runtime.InteropServices;

namespace Brows.Runtime.InteropServices.ComTypes {
    using Win32;

    [Guid(IID.ISharedBitmap)]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISharedBitmap {
        void Detach([Out] out IntPtr phbm);
        void GetFormat([Out] out WTSAT pat);
        void GetSharedBitmap([Out] out IntPtr phbm);
        void GetSize([Out] out SIZE pSize);
        void InitializeBitmap([In] IntPtr hbm, [In] WTSAT wtsAT);
    }
}
