using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace Brows.Native {
    public sealed class NativeSecureString : CriticalFinalizerObject, IDisposable {
        private byte[] GetBytes() {
            var bstr = Marshal.SecureStringToBSTR(SecureString);
            try {
                var s = Marshal.PtrToStringBSTR(bstr);
                var b = Encoding.GetBytes(s + '\0');
                return b;
            }
            finally {
                Marshal.ZeroFreeBSTR(bstr);
            }
        }

        private byte[] Bytes => _Bytes ??= GetBytes();
        private byte[] _Bytes;

        private GCHandle Pin => _Pin ??= GCHandle.Alloc(Bytes, GCHandleType.Pinned);
        private GCHandle? _Pin;

        private void Dispose(bool disposing) {
            if (_Pin.HasValue && _Pin.Value.IsAllocated) {
                _Pin.Value.Free();
            }
            if (_Bytes != null) {
                Array.Clear(_Bytes);
            }
        }

        public IntPtr Handle => _Handle ??= Pin.AddrOfPinnedObject();
        private IntPtr? _Handle;

        public Encoding Encoding { get; }
        public SecureString SecureString { get; }

        public NativeSecureString(Encoding encoding, SecureString secureString) {
            Encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
            SecureString = secureString;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~NativeSecureString() {
            Dispose(false);
        }
    }
}
