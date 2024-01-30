using Domore.Logs;
using Domore.Runtime.Win32;
using System.Runtime.InteropServices;

namespace Domore.Runtime.InteropServices {
    public static class HRESULTExtension {
        private static readonly ILog Log = Logging.For(typeof(HRESULTExtension));

        public static void ThrowOnError(this HRESULT hresult) {
            var hr = (uint)hresult;
            switch (hr) {
                case 0:
                    break;
                case 0x80270000:
                    if (Log.Info()) {
                        Log.Info($"User canceled (HRESULT {hresult})");
                    }
                    break;
                default:
                    Marshal.ThrowExceptionForHR(unchecked((int)hresult));
                    break;
            }
        }
    }
}
