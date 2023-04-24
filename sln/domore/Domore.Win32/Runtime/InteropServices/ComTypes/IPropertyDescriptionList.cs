using Domore.Runtime.Win32;
using System;
using System.Runtime.InteropServices;

namespace Domore.Runtime.InteropServices.ComTypes {
    [Guid(IID.IPropertyDescriptionList)]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyDescriptionList {
        [PreserveSig]
        HRESULT GetCount([Out] out uint pcElem);

        [PreserveSig]
        HRESULT GetAt(
            [In] uint iElem,
            [In] ref Guid riid,
            [Out, MarshalAs(UnmanagedType.Interface, IidParameterIndex = 1)] out IPropertyDescription ppv);
    };
}
