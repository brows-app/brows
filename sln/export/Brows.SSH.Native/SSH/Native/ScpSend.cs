using Brows.Native;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Brows.SSH.Native {
    public sealed class ScpSend : Chan {
        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern IntPtr brows_ssh_ScpSend_create(IntPtr conn, IntPtr path, int mode, long size);

        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern void brows_ssh_ScpSend_destroy(IntPtr p);

        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern int brows_ssh_ScpSend_write(IntPtr p, IntPtr buf, nuint buf_len, ref nuint result, ref BrowsCanceler cancel);

        private NativeString PathAlloc => _PathAlloc ??= Conn.Alloc(Path);
        private NativeString _PathAlloc;

        protected sealed override IntPtr Create() {
            return brows_ssh_ScpSend_create(Conn.GetHandle(), PathAlloc.Handle, Mode, Size);
        }

        protected sealed override void Dispose(bool disposing) {
            if (disposing) {
                using (_PathAlloc) {
                }
            }
            base.Dispose(disposing);
        }

        protected sealed override void Destroy(nint handle) {
            brows_ssh_ScpSend_destroy(handle);
        }

        public Conn Conn { get; }
        public string Path { get; }
        public long Size { get; }
        public int Mode { get; }

        public ScpSend(Conn conn, string path, int mode, long size) {
            Conn = conn ?? throw new ArgumentNullException(nameof(conn));
            Path = path;
            Mode = mode;
            Size = size;
        }

        public Stream Stream() {
            return new StreamImplementation(this);
        }

        private sealed class StreamImplementation : ChanStream {
            protected sealed override nuint NativeWrite(nint p, int streamID, nint buf, nuint bufLen, BrowsCanceler cancel) {
                var written = default(nuint);
                Agent.Try(() => brows_ssh_ScpSend_write(p, buf, bufLen, ref written, ref cancel));
                return written;
            }

            public sealed override bool CanWrite => true;

            public ScpSend Agent { get; }

            public StreamImplementation(ScpSend agent) : base(agent, 0) {
                Agent = agent ?? throw new ArgumentNullException(nameof(agent));
            }
        }
    }
}
