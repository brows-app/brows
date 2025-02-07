using Domore.Logs;
using System;
using System.Runtime.InteropServices;

namespace Brows.Url {
    internal sealed class ClientDataCallbackWrapper {
        private static readonly ILog Log = Logging.For(typeof(ClientDataCallbackWrapper));

        [UnmanagedFunctionPointer(UrlNative.Call)]
        private delegate int brows_url_ClientForUrl_DataCallback(IntPtr p, IntPtr buf, nint buf_len);

        private IntPtr Pointer;
        private Delegate Delegate;

        public Action<IntPtr> PointerCreated { get; set; }

        public ClientDataCallback Value {
            get => _Value;
            set {
                if (Pointer == IntPtr.Zero) {
                    Pointer = Marshal.GetFunctionPointerForDelegate(Delegate = new brows_url_ClientForUrl_DataCallback((p, buf, len) => {
                        try {
                            var callback = Value;
                            if (callback != null) {
                                var arr = default(ReadOnlySpan<byte>);
                                unsafe {
                                    arr = new ReadOnlySpan<byte>((byte*)buf, (int)len);
                                }
                                callback(arr);
                            }
                            return 0;
                        }
                        catch (Exception ex) {
                            if (Log.Error()) {
                                Log.Error(ex);
                            }
                            return -1;
                        }
                    }));
                    PointerCreated?.Invoke(Pointer);
                }
                _Value = value;
            }
        }
        private ClientDataCallback _Value;
    }
}
