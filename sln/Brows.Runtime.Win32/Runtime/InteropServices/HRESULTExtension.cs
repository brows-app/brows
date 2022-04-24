using System.Runtime.InteropServices;

namespace Brows.Runtime.InteropServices {
    using Win32;

    internal static class HRESULTExtension {
        public static void ThrowOnError(this HRESULT hresult) {
            if (hresult != HRESULT.S_OK) {
                Marshal.ThrowExceptionForHR(unchecked((int)hresult));
            }
        }
    }
}
