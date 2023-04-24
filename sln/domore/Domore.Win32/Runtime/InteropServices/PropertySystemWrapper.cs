using System;

namespace Domore.Runtime.InteropServices {
    using ComTypes;

    public sealed class PropertySystemWrapper : ComObjectWrapper<IPropertySystem> {
        protected override IPropertySystem Factory() {
            return (IPropertySystem)Activator.CreateInstance(CLSID.Managed.Type.PropertySystem);
        }

        public IPropertySystem PropertySystem =>
            ComObject();
    }
}
