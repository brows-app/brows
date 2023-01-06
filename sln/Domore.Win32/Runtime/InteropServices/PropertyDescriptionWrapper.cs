using System;

namespace Domore.Runtime.InteropServices {
    using ComTypes;
    using System.Runtime.InteropServices;
    using Win32;

    public class PropertyDescriptionWrapper : ComObjectWrapper<IPropertyDescription> {

        private readonly Guid _FmtID;
        private readonly uint _PID;
        private readonly string _Name;

        private IPropertyDescription PropertyDescription => ComObject();

        protected override IPropertyDescription Factory() {
            var iid = IID.Managed.IPropertyDescription;
            if (_Name != null) {
                var
                hr = propsys.PSGetPropertyDescriptionByName(_Name, ref iid, out var ppv);
                hr.ThrowOnError();
                return ppv;
            }
            else {
                var propkey = new PROPERTYKEY { fmtid = _FmtID, pid = _PID };
                var
                hr = propsys.PSGetPropertyDescription(ref propkey, ref iid, out var ppv);
                hr.ThrowOnError();
                return ppv;
            }
        }

        public PROPERTYKEY PropertyKey {
            get {
                var
                hr = PropertyDescription.GetPropertyKey(out var pkey);
                hr.ThrowOnError();
                return pkey;
            }
        }


        public string CanonicalName {
            get {
                var ppszName = IntPtr.Zero;
                try {
                    var
                    hr = PropertyDescription.GetCanonicalName(out ppszName);
                    hr.ThrowOnError();
                    return Marshal.PtrToStringUni(ppszName);
                }
                finally {
                    if (ppszName != IntPtr.Zero) {
                        Marshal.FreeCoTaskMem(ppszName);
                    }
                }
            }
        }

        public string DisplayName {
            get {
                var ppszName = IntPtr.Zero;
                try {
                    var
                    hr = PropertyDescription.GetDisplayName(out ppszName);
                    hr.ThrowOnError();
                    return Marshal.PtrToStringUni(ppszName);
                }
                finally {
                    if (ppszName != IntPtr.Zero) {
                        Marshal.FreeCoTaskMem(ppszName);
                    }
                }
            }
        }

        public string FormatForDisplay(PROPVARIANT propvar) {
            var
            hr = PropertyDescription.FormatForDisplay(ref propvar, PROPDESC_FORMAT_FLAGS.DEFAULT, out var ppszDisplay);
            hr.ThrowOnError();
            return Marshal.PtrToStringUni(ppszDisplay);
        }

        public PropertyDescriptionWrapper(string name) {
            _Name = name;
        }

        public PropertyDescriptionWrapper(Guid fmtid, uint pid) {
            _FmtID = fmtid;
            _PID = pid;
        }
    }
}
