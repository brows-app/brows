using Brows.Native;
using Brows.Url.Ftp;
using Domore.Logs;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Brows.Url {
    public sealed class ClientForUrl : NativeType {
        private static readonly ILog Log = Logging.For(typeof(ClientForUrl));

        [DllImport(UrlNative.Dll, CallingConvention = UrlNative.Call)]
        private static extern IntPtr brows_url_ClientForUrl_create(IntPtr url);

        [DllImport(UrlNative.Dll, CallingConvention = UrlNative.Call)]
        private static extern void brows_url_ClientForUrl_destroy(IntPtr p);

        [DllImport(UrlNative.Dll, CallingConvention = UrlNative.Call)]
        private static extern IntPtr brows_url_ClientForUrl_error_string(IntPtr p);

        [DllImport(UrlNative.Dll, CallingConvention = UrlNative.Call)]
        private static extern int brows_url_ClientForUrl_on_header(IntPtr p, IntPtr callback);

        [DllImport(UrlNative.Dll, CallingConvention = UrlNative.Call)]
        private static extern int brows_url_ClientForUrl_on_write(IntPtr p, IntPtr callback);

        [DllImport(UrlNative.Dll, CallingConvention = UrlNative.Call)]
        private static extern int brows_url_ClientForUrl_password(IntPtr p, IntPtr value);

        [DllImport(UrlNative.Dll, CallingConvention = UrlNative.Call)]
        private static extern int brows_url_ClientForUrl_txrx(IntPtr p);

        [DllImport(UrlNative.Dll, CallingConvention = UrlNative.Call)]
        private static extern int brows_url_ClientForUrl_username(IntPtr p, IntPtr value);

        private readonly ClientDataCallbackWrapper WriteCallback;
        private readonly ClientDataCallbackWrapper HeaderCallback;

        private string UriString => _UriString ??= Uri.ToString();
        private string _UriString;

        private NativeString UriAlloc => _UriAlloc ??= new(Encoding.UTF8, UriString);
        private NativeString _UriAlloc;

        internal new void Try(Func<IntPtr, int> action, Func<int, Exception> exceptionFactory = null) {
            base.Try(action, exceptionFactory);
        }

        protected sealed override void Dispose(bool disposing) {
            if (disposing) {
                using (_UriAlloc) {
                }
            }
            base.Dispose(disposing);
        }

        protected sealed override nint Create() {
            return brows_url_ClientForUrl_create(UriAlloc.Handle);
        }

        protected sealed override void Destroy(nint handle) {
            brows_url_ClientForUrl_destroy(handle);
        }

        public Encoding Encoding { get; } = Encoding.UTF8;

        public Uri Uri { get; }

        public ClientForUrl(Uri uri) {
            Uri = uri ?? throw new ArgumentNullException(nameof(uri));
            WriteCallback = new() { PointerCreated = p => Try(h => brows_url_ClientForUrl_on_write(h, p)) };
            HeaderCallback = new() { PointerCreated = p => Try(h => brows_url_ClientForUrl_on_header(h, p)) };
        }

        public void Username(string value) {
            using (var s = new NativeString(Encoding, value)) {
                Try(p => brows_url_ClientForUrl_username(p, s.Handle));
            }
        }

        public void Password(string value) {
            using (var s = new NativeString(Encoding, value)) {
                Try(p => brows_url_ClientForUrl_password(p, s.Handle));
            }
        }

        public void OnWrite(ClientDataCallback callback) {
            WriteCallback.Value = callback;
        }

        public void OnHeader(ClientDataCallback callback) {
            HeaderCallback.Value = callback;
        }

        public void TxRx() {
            Try(brows_url_ClientForUrl_txrx);
        }

        public string ErrorString() {
            var s = default(IntPtr);
            Try(p => {
                s = brows_url_ClientForUrl_error_string(p);
                return 0;
            });
            return Marshal.PtrToStringUTF8(s);
        }
    }
}
