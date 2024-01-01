using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Text;

namespace Brows.SSH.Native {
    public sealed class ConnString : CriticalFinalizerObject, IDisposable {
        private byte[] Bytes => _Bytes ?? (_Bytes = Encoding.GetBytes(String));
        private byte[] _Bytes;

        private GCHandle Pin => _Pin ?? (_Pin = GCHandle.Alloc(Bytes, GCHandleType.Pinned)).Value;
        private GCHandle? _Pin;

        private void Dispose(bool disposing) {
            if (_Pin.HasValue && _Pin.Value.IsAllocated) {
                _Pin.Value.Free();
            }
        }

        public IntPtr Handle => _Handle ?? (_Handle = Pin.AddrOfPinnedObject()).Value;
        private IntPtr? _Handle;

        public Encoding Encoding { get; }
        public string String { get; }

        public ConnString(Encoding encoding, string @string) {
            Encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
            String = @string;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ConnString() {
            Dispose(false);
        }
    }
}
