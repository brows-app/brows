using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Brows.Runtime.InteropServices.ComTypes {
    using Win32;

    [Guid(IID.IExtractImage)]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IExtractImage {
        [PreserveSig] HRESULT Extract(out IntPtr phBmpThumbnail);
        [PreserveSig]
        HRESULT GetLocation(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszPathBuffer,
            uint cch,
            ref IEI_PRIORITY pdwPriority,
            ref SIZE prgSize,
            uint dwRecClrDepth,
            ref IEIFLAG pdwFlags);
    }
}
