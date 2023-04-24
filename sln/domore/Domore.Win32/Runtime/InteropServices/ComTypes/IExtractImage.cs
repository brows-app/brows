using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Domore.Runtime.InteropServices.ComTypes {
    using Win32;

    [Guid(IID.IExtractImage)]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IExtractImage {
        [PreserveSig]
        HRESULT GetLocation(
            [Out, MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 1)] StringBuilder pszPathBuffer,
            [In] uint cch,
            [Out, In] ref IEI_PRIORITY pdwPriority,
            [In] ref SIZE prgSize,
            [In] uint dwRecClrDepth,
            [Out, In] ref IEIFLAG pdwFlags);

        [PreserveSig]
        HRESULT Extract([Out] out IntPtr phBmpThumbnail);
    }
}
