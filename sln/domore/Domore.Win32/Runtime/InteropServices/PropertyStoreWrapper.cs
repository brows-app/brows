using System;

namespace Domore.Runtime.InteropServices {
    using ComTypes;
    using Win32;

    public sealed class PropertyStoreWrapper : ComObjectWrapper<IPropertyStore> {
        protected sealed override IPropertyStore Factory() {
            var iid = IID.Managed.IPropertyStore;
            var flags = ReadOnly ? GETPROPERTYSTOREFLAGS.DEFAULT : GETPROPERTYSTOREFLAGS.READWRITE;
            var
            hr = shell32.SHGetPropertyStoreFromParsingName(Name, IntPtr.Zero, flags, ref iid, out var ppv);
            hr.ThrowOnError();
            return ppv;
        }

        public IPropertyStore PropertyStore
            => ComObject();

        public string Name { get; }
        public bool ReadOnly { get; }

        public PropertyStoreWrapper(string name, bool readOnly) {
            Name = name;
            ReadOnly = readOnly;
        }
    }
}
