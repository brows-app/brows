using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Brows.Runtime.InteropServices {
    using Win32;

    internal abstract class AssocQueryStringWrapper<T> : ComObjectWrapper<T> where T : class {
        protected abstract string IID { get; }

        protected sealed override T Factory() {
            var pszOut = new StringBuilder(1024);
            var pcchOut = (uint)pszOut.Capacity;
            var pszAssoc = Extension;
            var pszExtra = $"{{{IID}}}";
            var hr = shlwapi.AssocQueryStringW(
                ASSOCF.INIT_DEFAULTTOSTAR | ASSOCF.NOTRUNCATE,
                ASSOCSTR.SHELLEXTENSION,
                pszAssoc,
                pszExtra,
                pszOut,
                ref pcchOut);
            hr.ThrowOnError();

            var guid = new Guid(pszOut.ToString());
            var type = Type.GetTypeFromCLSID(guid);
            var inst = Activator.CreateInstance(type);
            var kind = inst as T;
            if (kind == null) {
                Marshal.FinalReleaseComObject(inst);
                throw new InvalidCastException();
            }
            return kind;
        }

        public string Extension { get; }

        public AssocQueryStringWrapper(string extension) {
            Extension = extension;
        }
    }
}
