using System.Runtime.InteropServices;

namespace Domore.Runtime.InteropServices {
    using Win32;

    public static class HRESULTExtension {
        public static void ThrowOnError(this HRESULT hresult) {
            Marshal.ThrowExceptionForHR(unchecked((int)hresult));
        }
    }
}
