using System;
using System.Runtime.InteropServices;

namespace Domore.Runtime.InteropServices.ComTypes {
    using Win32;

    [Guid(IID.IPreviewHandler)]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPreviewHandler {
        [PreserveSig]
        HRESULT SetWindow(IntPtr hwnd, ref RECT prc);

        [PreserveSig]
        HRESULT SetRect(ref RECT prc);

        [PreserveSig]
        HRESULT DoPreview();

        [PreserveSig]
        HRESULT Unload();

        [PreserveSig]
        HRESULT SetFocus();

        [PreserveSig]
        HRESULT QueryFocus(out IntPtr phwnd);
    }
}
