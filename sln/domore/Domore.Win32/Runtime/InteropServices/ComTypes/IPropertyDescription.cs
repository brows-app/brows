using System;
using System.Runtime.InteropServices;

namespace Domore.Runtime.InteropServices.ComTypes {
    using Win32;

    [Guid(IID.IPropertyDescription)]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyDescription {
        [PreserveSig] HRESULT GetPropertyKey([Out] out PROPERTYKEY pkey);
        [PreserveSig] HRESULT GetCanonicalName([Out] out IntPtr ppszName);
        [PreserveSig] HRESULT GetPropertyType();
        [PreserveSig] HRESULT GetDisplayName([Out] out IntPtr ppszName);
        [PreserveSig] HRESULT GetEditInvitation();
        [PreserveSig] HRESULT GetTypeFlags();
        [PreserveSig] HRESULT GetViewFlags();
        [PreserveSig] HRESULT GetDefaultColumnWidth([Out] out uint pcxChars);
        [PreserveSig] HRESULT GetDisplayType([Out] out PROPDESC_DISPLAYTYPE pdisplaytype);
        [PreserveSig] HRESULT GetColumnState();
        [PreserveSig] HRESULT GetGroupingRange();
        [PreserveSig] HRESULT GetRelativeDescriptionType();
        [PreserveSig] HRESULT GetRelativeDescription();
        [PreserveSig] HRESULT GetSortDescription([Out] out PROPDESC_SORTDESCRIPTION psd);
        [PreserveSig] HRESULT GetSortDescriptionLabel([In] bool fDescending, [Out] out IntPtr ppszDescription);
        [PreserveSig] HRESULT GetAggregationType([Out] out PROPDESC_AGGREGATION_TYPE paggtype);
        [PreserveSig] HRESULT GetConditionType();
        [PreserveSig] HRESULT GetEnumTypeList();
        [PreserveSig] HRESULT CoerceToCanonicalValue([In, Out] ref PROPVARIANT ppropvar);
        [PreserveSig]
        HRESULT FormatForDisplay(
            [In] ref PROPVARIANT propvar,
            [In] PROPDESC_FORMAT_FLAGS pdfFlags,
            [Out] out IntPtr ppszDisplay);
        [PreserveSig] HRESULT IsValueCanonical([In] ref PROPVARIANT propvar);
    }
}
