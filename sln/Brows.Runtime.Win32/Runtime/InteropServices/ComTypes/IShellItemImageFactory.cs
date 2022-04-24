using System;
using System.Runtime.InteropServices;

namespace Brows.Runtime.InteropServices.ComTypes {
    using Win32;

    [Guid(IID.IShellItemImageFactory)]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItemImageFactory {
        [PreserveSig] HRESULT GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
    }
}
