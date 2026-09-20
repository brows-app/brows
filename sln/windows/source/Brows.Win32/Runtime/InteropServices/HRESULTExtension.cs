using Brows.Runtime.Win32;
using Domore.Logs;
using System.Runtime.InteropServices;

namespace Brows.Runtime.InteropServices;

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
