using Brows.Native;
using System;
using System.Runtime.InteropServices;

namespace Brows.SSH.Native {
    public sealed class KnownHosts : NativeType {

        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern IntPtr brows_ssh_KnownHosts_create(IntPtr conn);

        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern void brows_ssh_KnownHosts_destroy(IntPtr p);

        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern int brows_ssh_KnownHosts_check(IntPtr p, IntPtr file, IntPtr name, int port, IntPtr knownHost);

        protected sealed override IntPtr Create() {
            return brows_ssh_KnownHosts_create(Conn.Handle());
        }

        protected override void Destroy(IntPtr handle) {
            brows_ssh_KnownHosts_destroy(handle);
        }

        public Conn Conn { get; }

        public KnownHosts(Conn conn) {
            Conn = conn ?? throw new ArgumentNullException(nameof(conn));
        }

        public void Check(string file, string name, int port, KnownHost knownHost) {
            if (null == knownHost) throw new ArgumentNullException(nameof(knownHost));
            using var pFile = NativeGlobalAlloc.String(file, CharSet.Ansi);
            using var pName = NativeGlobalAlloc.String(name, CharSet.Ansi);
            Try(() => brows_ssh_KnownHosts_check(GetHandle(), pFile.HGlobal, pName.HGlobal, port, knownHost.Handle));
        }
    }
}
