using System;
using System.Runtime.InteropServices;

namespace Brows.Runtime.InteropServices.ComTypes {
    using Win32;

    [Guid(IID.IPreviewHandler)]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPreviewHandler {
        [PreserveSig] HRESULT DoPreview();
        [PreserveSig] HRESULT QueryFocus(out IntPtr phwnd);
        [PreserveSig] HRESULT SetFocus();
        [PreserveSig] HRESULT SetRect(ref RECT prc);
        [PreserveSig] HRESULT SetWindow(IntPtr hwnd, ref RECT prc);
        [PreserveSig] HRESULT Unload();

        //[PreserveSig]
        //uint TranslateAccelerator(ref MSG pmsg);
    }
}
