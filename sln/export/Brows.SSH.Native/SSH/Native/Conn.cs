using Brows.Native;
using Domore.Logs;
using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace Brows.SSH.Native {
    public sealed class Conn : NativeType {
        private static readonly ILog Log = Logging.For(typeof(Conn));

        [UnmanagedFunctionPointer(SSHNative.Call)]
        private delegate int brows_ssh_Conn_Auth(IntPtr p, ref BrowsCanceler cancel);

        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern
            int brows_ssh_Conn_auth_by_password(IntPtr p, IntPtr password, ref BrowsCanceler cancel);
        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern
            int brows_ssh_Conn_auth_by_key_file(IntPtr p, IntPtr public_key_file, IntPtr private_key_file, IntPtr passphrase, ref BrowsCanceler cancel);
        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern
            int brows_ssh_Conn_close(IntPtr p, ref BrowsCanceler canceler);
        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern
            int brows_ssh_Conn_auth_success(IntPtr p);
        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern
            int brows_ssh_Conn_connect(IntPtr p, ref BrowsCanceler canceler);
        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern
            IntPtr brows_ssh_Conn_create();
        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern
            void brows_ssh_Conn_destroy(IntPtr p);
        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern
            IntPtr brows_ssh_Conn_get_fingerprint(IntPtr p);
        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern
            IntPtr brows_ssh_Conn_get_fingerprint_hash_func(IntPtr p);
        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern
            int brows_ssh_Conn_get_fingerprint_size(IntPtr p);
        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern
            long brows_ssh_Conn_get_host(IntPtr p);
        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern
            int brows_ssh_Conn_get_host_family(IntPtr p);
        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern
            int brows_ssh_Conn_get_port(IntPtr p);
        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern
            IntPtr brows_ssh_Conn_get_username(IntPtr p);
        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern
            int brows_ssh_Conn_set_host(IntPtr p, long v);
        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern
            int brows_ssh_Conn_set_host_family(IntPtr p, int v);
        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern
            int brows_ssh_Conn_set_port(IntPtr p, int v);
        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern
            int brows_ssh_Conn_set_username(IntPtr p, IntPtr v);

        private void UseBytes(SecureString secureString, Action<IntPtr> callback) {
            if (null == callback) throw new ArgumentNullException(nameof(callback));
            if (null == secureString) {
                callback(IntPtr.Zero);
                return;
            }
            var byts = default(byte[]);
            try {
                var bstr = Marshal.SecureStringToBSTR(secureString);
                try {
                    byts = Encoding.GetBytes(Marshal.PtrToStringBSTR(bstr));
                }
                finally {
                    Marshal.ZeroFreeBSTR(bstr);
                }
                var pin = GCHandle.Alloc(byts, GCHandleType.Pinned);
                try {
                    callback(pin.AddrOfPinnedObject());
                }
                finally {
                    pin.Free();
                }
            }
            finally {
                if (byts != null) {
                    for (var i = 0; i < byts.Length; i++) {
                        byts[i] = 0;
                    }
                }
            }
        }

        private void UseBytes(string s, Action<IntPtr> callback) {
            if (null == callback) throw new ArgumentNullException(nameof(callback));
            if (null == s) {
                callback(IntPtr.Zero);
                return;
            }
            var byt = Encoding.GetBytes(s);
            var pin = GCHandle.Alloc(byt, GCHandleType.Pinned);
            try {
                callback(pin.AddrOfPinnedObject());
            }
            finally {
                pin.Free();
            }
        }

        protected sealed override void Dispose(bool disposing) {
            try {
                Close();
            }
            catch (Exception ex) {
                if (Log.Error()) {
                    Log.Error(ex);
                }
            }
            base.Dispose(disposing);
        }

        protected sealed override IntPtr Create() {
            return brows_ssh_Conn_create();
        }

        protected sealed override void Destroy(IntPtr handle) {
            brows_ssh_Conn_destroy(handle);
        }

        public byte[] Fingerprint {
            get {
                var p = GetHandle();
                var src = brows_ssh_Conn_get_fingerprint(p);
                var size = FingerprintSize;
                var dest = new byte[size];
                Marshal.Copy(src, dest, 0, size);
                return dest;
            }
        }

        public int FingerprintSize =>
            GetInt32(brows_ssh_Conn_get_fingerprint_size);

        public string FingerprintHashFunc =>
            GetAnsiString(brows_ssh_Conn_get_fingerprint_hash_func);

        public long Host {
            get => GetInt64(brows_ssh_Conn_get_host);
            set => SetInt64(brows_ssh_Conn_set_host, value);
        }

        public int HostFamily {
            get => GetInt32(brows_ssh_Conn_get_host_family);
            set => SetInt32(brows_ssh_Conn_set_host_family, value);
        }

        public int Port {
            get => GetInt32(brows_ssh_Conn_get_port);
            set => SetInt32(brows_ssh_Conn_set_port, value);
        }

        public string Username {
            get => GetAnsiString(brows_ssh_Conn_get_username);
            set => SetAnsiString(brows_ssh_Conn_set_username, value);
        }

        public Encoding Encoding {
            get => _Encoding ?? (_Encoding = Encoding.UTF8);
            set => _Encoding = value;
        }
        private Encoding _Encoding;

        public IntPtr Handle() {
            return GetHandle();
        }

        public void Connect() {
            var canceler = BrowsCanceler.None;
            Try(() => brows_ssh_Conn_connect(GetHandle(), ref canceler));
        }

        public void Close() {
            var canceler = BrowsCanceler.None;
            Try(() => brows_ssh_Conn_close(GetHandle(), ref canceler));
        }

        public void AuthByPassword(SecureString password, BrowsCanceler cancel) {
            UseBytes(password, ptr => {
                Try(() => brows_ssh_Conn_auth_by_password(
                    p: GetHandle(),
                    password: ptr,
                    cancel: ref cancel), SSHException.Factory);
            });
        }

        public void AuthByKeyFile(string publicKeyFile, string privateKeyFile, SecureString passphrase, BrowsCanceler cancel) {
            UseBytes(passphrase, pPassphrase =>
            UseBytes(publicKeyFile, pPublicKeyFile =>
            UseBytes(privateKeyFile, pPrivateKeyFile => {
                Try(() => brows_ssh_Conn_auth_by_key_file(
                    p: GetHandle(),
                    public_key_file: pPublicKeyFile,
                    private_key_file: pPrivateKeyFile,
                    passphrase: pPassphrase,
                    cancel: ref cancel), SSHException.Factory);
            })));
        }

        public ConnString Alloc(string s) {
            return new ConnString(Encoding, s);
        }

        public bool AuthSuccess() {
            try {
                Try(brows_ssh_Conn_auth_success);
            }
            catch {
                return false;
            }
            return true;
        }
    }
}
