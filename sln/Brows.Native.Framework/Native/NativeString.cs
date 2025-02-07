using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Text;

namespace Brows.Native {
    public sealed class NativeString : CriticalFinalizerObject, IDisposable {
        private byte[] Bytes => _Bytes ??= Encoding.GetBytes(String + '\0');
        private byte[] _Bytes;

        private GCHandle Pin => _Pin ??= GCHandle.Alloc(Bytes, GCHandleType.Pinned);
        private GCHandle? _Pin;

        private void Dispose(bool disposing) {
            if (_Pin.HasValue && _Pin.Value.IsAllocated) {
                _Pin.Value.Free();
            }
        }

        public IntPtr Handle => _Handle ??= Pin.AddrOfPinnedObject();
        private IntPtr? _Handle;

        public string String { get; }
        public Encoding Encoding { get; }

        public NativeString(Encoding encoding, string @string) {
            Encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
            String = @string;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~NativeString() {
            Dispose(false);
        }
    }
}
