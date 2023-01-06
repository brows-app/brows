using System;
using System.Runtime.InteropServices;

namespace Domore.Runtime.Win32 {
    using InteropServices.ComTypes;

    public static class propsys {
        [DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        public static extern HRESULT PSGetPropertyDescriptionByName(
            [In] string pszCanonicalName,
            [In] ref Guid riid,
            [Out][MarshalAs(UnmanagedType.Interface, IidParameterIndex = 1)] out IPropertyDescription ppv);

        [DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        public static extern HRESULT PSGetPropertyDescription(
          [In] ref PROPERTYKEY propkey,
          [In] ref Guid riid,
          [Out][MarshalAs(UnmanagedType.Interface, IidParameterIndex = 1)] out IPropertyDescription ppv);
    }
}
