using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Domore.Runtime.InteropServices.ComTypes {
    using Win32;

    [Guid(IID.IPropertySystem)]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertySystem {
        [PreserveSig]
        HRESULT GetPropertyDescription(
            [In] ref PROPERTYKEY propkey,
            [In] ref Guid riid,
            [Out][MarshalAs(UnmanagedType.Interface, IidParameterIndex = 1)] out IPropertyDescription ppv);

        [PreserveSig]
        HRESULT GetPropertyDescriptionByName(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszCanonicalName,
            [In] ref Guid riid,
            [Out][MarshalAs(UnmanagedType.Interface, IidParameterIndex = 1)] out IPropertyDescription ppv);

        [PreserveSig]
        HRESULT GetPropertyDescriptionListFromString(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszPropList,
            [In] ref Guid riid,
            [Out][MarshalAs(UnmanagedType.Interface, IidParameterIndex = 1)] out IPropertyDescriptionList ppv);

        [PreserveSig]
        HRESULT EnumeratePropertyDescriptions(
            [In] PROPDESC_ENUMFILTER filterOn,
            [In] ref Guid riid,
            [Out][MarshalAs(UnmanagedType.Interface, IidParameterIndex = 1)] out IPropertyDescriptionList ppv);

        [PreserveSig]
        HRESULT FormatForDisplay(
            [In] ref PROPERTYKEY key,
            [In] ref PROPVARIANT propvar,
            [In] PROPDESC_FORMAT_FLAGS pdff,
            [Out][MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 4)] StringBuilder pszText,
            [In] uint cchText);

        [PreserveSig]
        HRESULT FormatForDisplayAlloc(
            [In] ref PROPERTYKEY key,
            [In] ref PROPVARIANT propvar,
            [In] PROPDESC_FORMAT_FLAGS pdff,
            [Out] out IntPtr ppszDisplay);

        [PreserveSig]
        HRESULT RegisterPropertySchema([In][MarshalAs(UnmanagedType.LPWStr)] string pszPath);

        [PreserveSig]
        HRESULT UnregisterPropertySchema([In][MarshalAs(UnmanagedType.LPWStr)] string pszPath);

        [PreserveSig]
        HRESULT RefreshPropertySchema();
    }
}
