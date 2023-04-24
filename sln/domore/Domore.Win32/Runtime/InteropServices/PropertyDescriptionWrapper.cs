using System;

namespace Domore.Runtime.InteropServices {
    using ComTypes;
    using Win32;

    public class PropertyDescriptionWrapper : ComObjectWrapper<IPropertyDescription> {
        private readonly Guid _FmtID;
        private readonly uint _PID;
        private readonly string _Name;

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

        public IPropertyDescription PropertyDescription =>
            ComObject();

        public PropertyDescriptionWrapper(string name) {
            _Name = name;
        }

        public PropertyDescriptionWrapper(Guid fmtid, uint pid) {
            _FmtID = fmtid;
            _PID = pid;
        }
    }
}
