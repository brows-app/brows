﻿using Domore.Runtime.InteropServices.ComTypes;
using System;
using System.Runtime.InteropServices;

namespace Domore.Runtime.Win32 {
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

        [DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        public static extern HRESULT PSGetNameFromPropertyKey(
            [In] ref PROPERTYKEY propkey,
            [Out] out IntPtr ppszCanonicalName);

        [DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        public static extern HRESULT PSGetPropertyKeyFromName(
            [In] string pszName,
            [Out] out PROPERTYKEY ppropkey);

        [DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        public static extern HRESULT PSFormatForDisplayAlloc(
            [In] ref PROPERTYKEY key,
            [In] ref PROPVARIANT propvar,
            [In] PROPDESC_FORMAT_FLAGS pdff,
            [Out] out IntPtr ppszDisplay);

        [DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        public static extern HRESULT PSEnumeratePropertyDescriptions(
            [In] PROPDESC_ENUMFILTER filterOn,
            [In] ref Guid riid,
            [Out, MarshalAs(UnmanagedType.Interface, IidParameterIndex = 1)] out IPropertyDescriptionList ppv);

        [DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        public static extern HRESULT PSCoerceToCanonicalValue(
          [In] ref PROPERTYKEY key,
          [In, Out] ref PROPVARIANT ppropvar);

        [DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        public static extern HRESULT PropVariantToVariant(
            [In] ref PROPVARIANT pPropVar,
            [Out] IntPtr pVar);

        [DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        public static extern HRESULT VariantToPropVariant(
            [In] IntPtr pVar,
            [Out] out PROPVARIANT pPropVar);
    }
}
