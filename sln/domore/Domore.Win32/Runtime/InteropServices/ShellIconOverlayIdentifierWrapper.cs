using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Domore.Runtime.InteropServices {
    using ComTypes;
    using Runtime.Win32;

    public sealed class ShellIconOverlayIdentifierWrapper : ComObjectWrapper<IShellIconOverlayIdentifier> {
        private IShellIconOverlayIdentifier Identifier =>
            ComObject();

        protected sealed override IShellIconOverlayIdentifier Factory() {
            try {
                var typ = Type.GetTypeFromCLSID(CLSID, throwOnError: false);
                var obj = typ == null ? null : Activator.CreateInstance(typ);
                var inst = obj as IShellIconOverlayIdentifier;
                if (inst == null) {
                    if (obj != null) {
                        Marshal.FinalReleaseComObject(obj);
                    }
                    throw new InvalidCastException();
                }
                return inst;
            }
            catch {
                return new CouldNotCreate();
            }
        }

        public string Name { get; }
        public Guid CLSID { get; }

        public ShellIconOverlayIdentifierWrapper(string name, Guid clsid) {
            Name = name;
            CLSID = clsid;
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

        private class CouldNotCreate : IShellIconOverlayIdentifier {
            HRESULT IShellIconOverlayIdentifier.IsMemberOf(string pwszPath, uint dwAttrib) {
                return HRESULT.S_FALSE;
            }

            HRESULT IShellIconOverlayIdentifier.GetOverlayInfo(StringBuilder pwszIconFile, int cchMax, out int pIndex, out ISIOI pdwFlags) {
                pIndex = 0;
                pdwFlags = ISIOI.ICONFILE;
                return HRESULT.S_OK;
            }

            HRESULT IShellIconOverlayIdentifier.GetPriority(out int pPriority) {
                pPriority = int.MaxValue;
                return HRESULT.S_OK;
            }
        }
    }
}
