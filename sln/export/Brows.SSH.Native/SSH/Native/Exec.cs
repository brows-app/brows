using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Brows.SSH.Native {
    public sealed class Exec : Chan {
        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern IntPtr brows_ssh_Exec_create(IntPtr conn);

        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern void brows_ssh_Exec_destroy(IntPtr p);

        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern int brows_ssh_Exec_read(IntPtr p, int stream_id, IntPtr buf, nuint buf_len, ref nuint result, ref BrowsCanceler cancel);

        protected sealed override IntPtr Create() {
            return brows_ssh_Exec_create(Conn.GetHandle());
        }

        protected sealed override void Destroy(IntPtr handle) {
            brows_ssh_Exec_destroy(handle);
        }

        public Stream StdOut =>
            _StdOut ?? (
            _StdOut = new StreamImplementation(this, streamID: 0));
        private Stream _StdOut;

        public Stream StdErr =>
            _StdErr ?? (
            _StdErr = new StreamImplementation(this, streamID: 1));
        private Stream _StdErr;

        public Conn Conn { get; }

        public Exec(Conn conn) {
            Conn = conn ?? throw new ArgumentNullException(nameof(conn));
        }

        private sealed class StreamImplementation : ChanStream {
            protected override nuint NativeRead(nint p, int streamID, nint buf, nuint bufLen, BrowsCanceler cancel) {
                var read = default(nuint);
                Agent.Try(() => brows_ssh_Exec_read(p, streamID, buf, bufLen, ref read, ref cancel));
                return read;
            }

            public sealed override bool CanRead => true;

            public Exec Agent { get; }

            public StreamImplementation(Exec agent, int streamID) : base(agent, streamID) {
                Agent = agent ?? throw new ArgumentNullException(nameof(agent));
            }
        }
    }
}
