using System.Runtime.InteropServices;

namespace Domore.Runtime.InteropServices.ComTypes {
    using Win32;

    [Guid(IID.IPropertyStore)]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyStore {
        [PreserveSig]
        HRESULT GetCount([Out] out uint cProps);

        [PreserveSig]
        HRESULT GetAt([In] uint iProp, [Out] out PROPERTYKEY pkey);

        [PreserveSig]
        HRESULT GetValue([In] ref PROPERTYKEY key, [In, Out] ref PROPVARIANT pv);

        [PreserveSig]
        HRESULT SetValue([In] ref PROPERTYKEY key, [In] ref object propvar);

        [PreserveSig]
        HRESULT Commit();
    }
}
