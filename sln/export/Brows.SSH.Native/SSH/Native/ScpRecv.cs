using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Brows.SSH.Native {
    public sealed class ScpRecv : Chan {
        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern IntPtr brows_ssh_ScpRecv_create(IntPtr conn, IntPtr path);

        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern void brows_ssh_ScpRecv_destroy(IntPtr p);

        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern long brows_ssh_ScpRecv_get_length(IntPtr p);

        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern long brows_ssh_ScpRecv_get_position(IntPtr p);

        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern int brows_ssh_ScpRecv_read(IntPtr p, IntPtr buf, nuint buf_len, ref nuint result, ref BrowsCanceler cancel);

        private ConnString PathAlloc =>
            _PathAlloc ?? (
            _PathAlloc = Conn.Alloc(Path));
        private ConnString _PathAlloc;

        protected sealed override IntPtr Create() {
            return brows_ssh_ScpRecv_create(Conn.GetHandle(), PathAlloc.Handle);
        }

        protected sealed override void Dispose(bool disposing) {
            if (disposing) {
                PathAlloc.Dispose();
            }
            base.Dispose(disposing);
        }

        protected sealed override void Destroy(nint handle) {
            brows_ssh_ScpRecv_destroy(handle);
        }

        public long Length =>
            brows_ssh_ScpRecv_get_length(GetHandle());

        public long Position =>
            brows_ssh_ScpRecv_get_position(GetHandle());

        public Conn Conn { get; }
        public string Path { get; }

        public ScpRecv(Conn conn, string path) {
            Conn = conn ?? throw new ArgumentNullException(nameof(conn));
            Path = path;
        }

        public Stream Stream() {
            return new StreamImplementation(this);
        }

        private sealed class StreamImplementation : ChanStream {
            protected sealed override nuint NativeRead(nint p, int streamID, nint buf, nuint bufLen, BrowsCanceler cancel) {
                var read = default(nuint);
                Agent.Try(() => brows_ssh_ScpRecv_read(p, buf, bufLen, ref read, ref cancel));
                return read;
            }

            public sealed override bool CanRead => true;
            public sealed override long Length => Agent.Length;
            public sealed override long Position {
                get => Agent.Position;
                set => throw new NotSupportedException();
            }

            public ScpRecv Agent { get; }

            public StreamImplementation(ScpRecv agent) : base(agent, 0) {
                Agent = agent ?? throw new ArgumentNullException(nameof(agent));
            }
        }
    }
}
