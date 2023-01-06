using System;
using System.Runtime.InteropServices;

namespace Domore.Runtime.InteropServices.ComTypes {
    using Win32;

    [Guid(IID.IPropertyDescription)]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyDescription {
        [PreserveSig]
        HRESULT GetPropertyKey([Out] out PROPERTYKEY pkey);

        [PreserveSig]
        HRESULT GetCanonicalName([Out] out IntPtr ppszName);

        [PreserveSig]
        HRESULT GetDisplayName([Out] out IntPtr ppszName);

        [PreserveSig]
        HRESULT FormatForDisplay(
            [In] ref PROPVARIANT propvar,
            [In] PROPDESC_FORMAT_FLAGS pdfFlags,
            [Out] out IntPtr ppszDisplay);
    }
}
