using System;
using System.Runtime.InteropServices;

namespace Domore.Runtime.InteropServices.ComTypes {
    using Win32;

    [Guid(IID.IObjectWithSite)]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IObjectWithSite {
        [PreserveSig]
        HRESULT SetSite([In, MarshalAs(UnmanagedType.IUnknown)] object pUnkSite);
    }
}
