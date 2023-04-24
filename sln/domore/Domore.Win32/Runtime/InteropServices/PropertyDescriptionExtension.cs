using System;
using System.Runtime.InteropServices;

namespace Domore.Runtime.InteropServices {
    using ComTypes;
    using Win32;

    public static class PropertyDescriptionExtension {
        public static PROPERTYKEY GetPropertyKey(this IPropertyDescription propertyDescription) {
            if (null == propertyDescription) throw new ArgumentNullException(nameof(propertyDescription));
            var
            hr = propertyDescription.GetPropertyKey(out var pkey);
            hr.ThrowOnError();
            return pkey;
        }


        public static string GetCanonicalName(this IPropertyDescription propertyDescription) {
            if (null == propertyDescription) throw new ArgumentNullException(nameof(propertyDescription));
            var ppszName = IntPtr.Zero;
            try {
                var
                hr = propertyDescription.GetCanonicalName(out ppszName);
                hr.ThrowOnError();
                return Marshal.PtrToStringUni(ppszName);
            }
            finally {
                if (ppszName != IntPtr.Zero) {
                    Marshal.FreeCoTaskMem(ppszName);
                }
            }
        }

        public static string GetDisplayName(this IPropertyDescription propertyDescription) {
            if (null == propertyDescription) throw new ArgumentNullException(nameof(propertyDescription));
            var ppszName = IntPtr.Zero;
            try {
                var hr = propertyDescription.GetDisplayName(out ppszName);
                if (hr == HRESULT.E_FAIL) {
                    return null;
                }
                else {
                    hr.ThrowOnError();
                }
                return Marshal.PtrToStringUni(ppszName);
            }
            finally {
                if (ppszName != IntPtr.Zero) {
                    Marshal.FreeCoTaskMem(ppszName);
                }
            }
        }

        public static string FormatForDisplay(this IPropertyDescription propertyDescription, PROPVARIANT propvar) {
            if (null == propertyDescription) throw new ArgumentNullException(nameof(propertyDescription));
            var ppszDisplay = IntPtr.Zero;
            try {
                var
                hr = propertyDescription.FormatForDisplay(ref propvar, PROPDESC_FORMAT_FLAGS.DEFAULT, out ppszDisplay);
                hr.ThrowOnError();
                return Marshal.PtrToStringUni(ppszDisplay);
            }
            finally {
                if (ppszDisplay != IntPtr.Zero) {
                    Marshal.FreeCoTaskMem(ppszDisplay);
                }
            }
        }
    }
}
