using Brows.Native;
using Domore.Logs;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Brows.SSH.Native {
    public abstract class Chan : NativeType {
        private static readonly ILog Log = Logging.For(typeof(Chan));

        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern int brows_ssh_Chan_eof(IntPtr p, ref int result, ref BrowsCanceler cancel);

        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern int brows_ssh_Chan_exec(IntPtr p, IntPtr command, ref BrowsCanceler cancel);

        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern int brows_ssh_Chan_flush(IntPtr p, int stream_id, ref BrowsCanceler cancel);

        public Encoding Encoding {
            get => _Encoding ?? (_Encoding = Encoding.UTF8);
            set => _Encoding = value;
        }
        private Encoding _Encoding;

        public bool EOF(BrowsCanceler cancel) {
            var result = default(int);
            Try(() => brows_ssh_Chan_eof(GetHandle(), ref result, ref cancel));
            return result != 0;
        }

        public void Exec(string command, BrowsCanceler cancel) {
            var byt = Encoding.GetBytes(command);
            var pin = GCHandle.Alloc(byt, GCHandleType.Pinned);
            try {
                Try(() => brows_ssh_Chan_exec(GetHandle(), pin.AddrOfPinnedObject(), ref cancel));
            }
            finally {
                pin.Free();
            }
        }

        public void Flush(int streamID, BrowsCanceler cancel) {
            if (Log.Debug()) {
                Log.Debug(Log.Join(nameof(Flush), streamID));
            }
            Try(() => brows_ssh_Chan_flush(GetHandle(), streamID, ref cancel));
        }
    }
}
