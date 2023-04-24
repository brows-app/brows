using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Domore.Runtime.Win32 {
    using InteropServices;
    using InteropServices.ComTypes;

    public static class PropertySystem {
        public static IEnumerable<PropertyDescription> EnumeratePropertyDescriptions() {
            var pdl = default(IPropertyDescriptionList);
            var pdlIID = IID.Managed.IPropertyDescriptionList;
            var
            hr = propsys.PSEnumeratePropertyDescriptions(PROPDESC_ENUMFILTER.VIEWABLE, ref pdlIID, out pdl);
            hr.ThrowOnError();
            try {
                hr = pdl.GetCount(out var pcElem);
                hr.ThrowOnError();
                for (uint i = 0; i < pcElem; i++) {
                    var pd = default(IPropertyDescription);
                    var pdIID = IID.Managed.IPropertyDescription;
                    hr = pdl.GetAt(i, ref pdIID, out pd);
                    hr.ThrowOnError();
                    try {
                        var item = PropertyDescription.Of(pd);
                        if (item != null) {
                            yield return item;
                        }
                    }
                    finally {
                        Marshal.ReleaseComObject(pd);
                    }
                }
            }
            finally {
                Marshal.ReleaseComObject(pdl);
            }
        }

        public static IEnumerable<PropertyDescription> EnumeratePropertyDescriptions(string fileName) {
            using var wrap = new PropertyStoreWrapper(fileName);
            var iid = IID.Managed.IPropertyDescription;
            var
            hr = wrap.PropertyStore.GetCount(out var cProps);
            hr.ThrowOnError();
            for (uint i = 0; i < cProps; i++) {
                hr = wrap.PropertyStore.GetAt(i, out var pkey);
                hr.ThrowOnError();
                if (pkey.pid < 2) {
                    continue;
                }
                var e = hr = propsys.PSGetPropertyDescription(ref pkey, ref iid, out var ppv);
                if (e == HRESULT.TYPE_E_ELEMENTNOTFOUND) {
                    continue;
                }
                else {
                    hr.ThrowOnError();
                }
                try {
                    var item = PropertyDescription.Of(ppv);
                    if (item != null) {
                        yield return item;
                    }
                }
                finally {
                    Marshal.ReleaseComObject(ppv);
                }
            }
        }

        public static IEnumerable<PropertyValue> EnumeratePropertyValues(string fileName, IEnumerable<PropertyDescription> propertyDescriptions, bool throwOnError) {
            if (null == propertyDescriptions) throw new ArgumentNullException(nameof(propertyDescriptions));
            using var wrap = new PropertyStoreWrapper(fileName);
            var propertyStore = default(IPropertyStore);
            try {
                propertyStore = wrap.PropertyStore;
            }
            catch {
                if (throwOnError) {
                    throw;
                }
                yield break;
            }
            foreach (var propertyDescription in propertyDescriptions) {
                var key = propertyDescription.Key;
                var pv = default(PROPVARIANT);
                var
                hr = wrap.PropertyStore.GetValue(ref key, ref pv);
                hr.ThrowOnError();
                try {
                    hr = propsys.PSFormatForDisplayAlloc(ref key, ref pv, PROPDESC_FORMAT_FLAGS.DEFAULT, out var ppszDisplay);
                    hr.ThrowOnError();
                    try {
                        yield return new(propertyDescription, Marshal.PtrToStringUni(ppszDisplay));
                    }
                    finally {
                        ole32.CoTaskMemFree(ppszDisplay);
                    }
                }
                finally {
                    ole32.PropVariantClear(ref pv);
                }
            }
        }

        public static PropertyDescription GetPropertyDescription(string name) {
            var iid = IID.Managed.IPropertyDescription;
            var hr = propsys.PSGetPropertyDescriptionByName(name, ref iid, out var ppv);
            hr.ThrowOnError();
            try {
                return PropertyDescription.Of(ppv);
            }
            finally {
                Marshal.ReleaseComObject(ppv);
            }
        }
    }
}
