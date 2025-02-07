using System;
using System.Runtime.InteropServices;

namespace Brows.Url.Ftp {
    public static class FtpExtension {
        [DllImport(UrlNative.Dll, CallingConvention = UrlNative.Call)]
        private static extern int brows_url_ClientForUrl_ftp_file_method(IntPtr p, FtpFileMethod value);

        public static void FtpFileMethod(this ClientForUrl clientForUrl, FtpFileMethod value) {
            ArgumentNullException.ThrowIfNull(clientForUrl);
            clientForUrl.Try(p => brows_url_ClientForUrl_ftp_file_method(p, value));
        }
    }
}
