using Domore.Runtime.InteropServices;
using Domore.Runtime.InteropServices.ComTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Domore.Runtime.Win32 {
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

        public static IEnumerable<PropertyDescription> EnumeratePropertyDescriptions(string fileName, bool throwOnError) {
            using var wrap = new PropertyStoreWrapper(fileName, readOnly: true);
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
            var iid = IID.Managed.IPropertyDescription;
            var
            hr = propertyStore.GetCount(out var cProps);
            hr.ThrowOnError();
            for (uint i = 0; i < cProps; i++) {
                hr = propertyStore.GetAt(i, out var pkey);
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

        public static PropertyValue GetPropertyValue(string fileName, PropertyDescription propertyDescription, bool throwOnError) {
            return EnumeratePropertyValues(fileName, new[] { propertyDescription }, throwOnError).FirstOrDefault();
        }

        public static IEnumerable<PropertyValue> EnumeratePropertyValues(string fileName, IEnumerable<PropertyDescription> propertyDescriptions, bool throwOnError) {
            if (null == propertyDescriptions) throw new ArgumentNullException(nameof(propertyDescriptions));
            using var wrap = new PropertyStoreWrapper(fileName, readOnly: true);
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
                hr = propertyStore.GetValue(ref key, ref pv);
                hr.ThrowOnError();
                try {
                    var variantObj = default(object);
                    var variant = Marshal.AllocCoTaskMem(Environment.Is64BitProcess ? 24 : 16);
                    try {
                        oleaut32.VariantInit(variant);
                        try {
                            hr = propsys.PropVariantToVariant(ref pv, variant);
                            hr.ThrowOnError();
                            try {
                                variantObj = Marshal.GetObjectForNativeVariant(variant);
                            }
                            catch (InvalidOleVariantTypeException) {
                            }
                        }
                        finally {
                            oleaut32.VariantClear(variant);
                        }
                    }
                    finally {
                        Marshal.FreeCoTaskMem(variant);
                    }
                    hr = propsys.PSFormatForDisplayAlloc(ref key, ref pv, PROPDESC_FORMAT_FLAGS.DEFAULT, out var ppszDisplay);
                    hr.ThrowOnError();
                    try {
                        yield return new PropertyValue(propertyDescription, Marshal.PtrToStringUni(ppszDisplay), variantObj);
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

        public static void SetPropertyValue(string fileName, PropertyDescription propertyDescription, object propertyValue) {
            SetPropertyValues(fileName, new[] { KeyValuePair.Create(propertyDescription, propertyValue) }, ignoreError: null);
        }

        public static void SetPropertyValues(string fileName, IEnumerable<KeyValuePair<PropertyDescription, object>> propertyValues, Func<KeyValuePair<PropertyDescription, object>, HRESULT, bool> ignoreError) {
            if (null == propertyValues) throw new ArgumentNullException(nameof(propertyValues));
            using var wrap = new PropertyStoreWrapper(fileName, readOnly: false);
            var propertyStore = wrap.PropertyStore;
            var hr = default(HRESULT);
            foreach (var propertyValue in propertyValues) {
                var propertyDescription = propertyValue.Key;
                var key = propertyDescription.Key;
                var obj = propertyValue.Value;
                var variant = Marshal.AllocCoTaskMem(Environment.Is64BitProcess ? 24 : 16);
                try {
                    Marshal.GetNativeVariantForObject(obj, variant);
                    try {
                        hr = propsys.VariantToPropVariant(variant, out var propVariant);
                        hr.ThrowOnError();
                        try {
                            hr = propsys.PSCoerceToCanonicalValue(ref key, ref propVariant);
                            hr.ThrowOnError();
                            try {
                                hr = propertyStore.SetValue(ref key, ref propVariant);
                                hr.ThrowOnError();
                            }
                            catch (Exception ex) {
                                var error = (HRESULT)unchecked((uint)ex.HResult);
                                var ignore = ignoreError != null && ignoreError(propertyValue, error);
                                if (ignore == false) {
                                    throw;
                                }
                            }
                        }
                        finally {
                            ole32.PropVariantClear(ref propVariant);
                        }
                    }
                    finally {
                        oleaut32.VariantClear(variant);
                    }
                }
                finally {
                    Marshal.FreeCoTaskMem(variant);
                }
            }
            hr = propertyStore.Commit();
            hr.ThrowOnError();
        }

        public static void TransferPropertyValues(string sourceFileName, string destinationFileName, Func<KeyValuePair<PropertyDescription, object>, bool> ignoreProperty, Func<KeyValuePair<PropertyDescription, object>, HRESULT, bool> ignoreError) {
            var set = new Dictionary<PropertyDescription, object>();
            var sourceProperties = EnumeratePropertyDescriptions(sourceFileName, throwOnError: true);
            var propertyDescriptions = sourceProperties.ToList();
            var propertyValues = EnumeratePropertyValues(sourceFileName, propertyDescriptions, throwOnError: true);
            foreach (var propertyValue in propertyValues) {
                var key = propertyValue.Description;
                var val = propertyValue.Object;
                if (ignoreProperty == null || ignoreProperty(KeyValuePair.Create(key, val)) == false) {
                    set[key] = val;
                }
            }
            SetPropertyValues(destinationFileName, set, ignoreError);
        }
    }
}
