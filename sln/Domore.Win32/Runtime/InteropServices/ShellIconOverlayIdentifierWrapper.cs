using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Domore.Runtime.InteropServices {
    using ComTypes;
    using Runtime.Win32;

    public sealed class ShellIconOverlayIdentifierWrapper : ComObjectWrapper<IShellIconOverlayIdentifier> {
        protected sealed override IShellIconOverlayIdentifier Factory() {
            return Identifier;
        }

        public IShellIconOverlayIdentifier Identifier { get; private set; }

        public string Name { get; }
        public Guid CLSID { get; }

        public ShellIconOverlayIdentifierWrapper(string name, Guid clsid) {
            Name = name;
            CLSID = clsid;
            var typ = Type.GetTypeFromCLSID(CLSID, throwOnError: false);
            var obj = typ == null ? null : Activator.CreateInstance(typ);
            var inst = obj as IShellIconOverlayIdentifier;
            if (inst == null) {
                if (obj != null) {
                    Marshal.FinalReleaseComObject(obj);
                }
                throw new InvalidCastException();
            }
            Identifier = inst;
        }

        public int GetPriority() {
            var id = Identifier;
            var pr = default(int);
            var hr = id.GetPriority(out pr);
            hr.ThrowOnError();
            return pr;
        }

        public bool IsMemberOf(string path) {
            var id = Identifier;
            var hr = id.IsMemberOf(path, 0);
            switch (hr) {
                case HRESULT.S_OK:
                    return true;
                case HRESULT.S_FALSE:
                    return false;
                default:
                    hr.ThrowOnError();
                    return false;
            }
        }

        public string GetOverlayInfo(out int index, out ISIOI flags) {
            var id = Identifier;
            var sb = new StringBuilder(capacity: 255, maxCapacity: 255);
            var hr = id.GetOverlayInfo(sb, 255, out index, out flags);
            hr.ThrowOnError();
            return sb.ToString();
        }
    }
}
