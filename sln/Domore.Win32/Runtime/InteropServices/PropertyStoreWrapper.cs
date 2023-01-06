using System;

namespace Domore.Runtime.InteropServices {
    using ComTypes;
    using Win32;

    public sealed class PropertyStoreWrapper : ComObjectWrapper<IPropertyStore> {
        protected sealed override IPropertyStore Factory() {
            var iid = IID.Managed.IPropertyStore;
            var
            hr = shell32.SHGetPropertyStoreFromParsingName(Name, IntPtr.Zero, GETPROPERTYSTOREFLAGS.GPS_DEFAULT, ref iid, out var ppv);
            hr.ThrowOnError();
            return ppv;
        }

        public IPropertyStore PropertyStore
            => ComObject();

        public string Name { get; }

        public PropertyStoreWrapper(string name) {
            Name = name;
        }
    }
}
