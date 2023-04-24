using System;
using System.Runtime.InteropServices;

namespace Domore.Runtime.InteropServices.ComTypes {
    using Win32;

    [Guid(IID.IPreviewHandlerVisuals)]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPreviewHandlerVisuals {
        [PreserveSig]
        HRESULT SetBackgroundColor(uint color);

        [PreserveSig]
        HRESULT SetFont(ref LOGFONTW plf);

        [PreserveSig]
        HRESULT SetTextColor(uint color);
    }
}
