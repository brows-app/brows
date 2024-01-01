using Brows.Native;
using System;
using System.Runtime.InteropServices;

namespace Brows.SSH.Native {
    public sealed class KnownHost : NativeType {
        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern IntPtr brows_ssh_KnownHost_create();

        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern void brows_ssh_KnownHost_destroy(IntPtr p);

        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern IntPtr brows_ssh_KnownHost_get_key_base64(IntPtr p);

        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern IntPtr brows_ssh_KnownHost_get_name_plain(IntPtr p);

        [DllImport(SSHNative.Dll, CallingConvention = SSHNative.Call)]
        private static extern KnownHostStatus brows_ssh_KnownHost_get_status(IntPtr p);

        internal IntPtr Handle => GetHandle();

        protected sealed override IntPtr Create() {
            return brows_ssh_KnownHost_create();
        }

        protected sealed override void Destroy(IntPtr handle) {
            brows_ssh_KnownHost_destroy(handle);
        }

        public string KeyBase64 =>
            GetAnsiString(brows_ssh_KnownHost_get_key_base64);

        public string NamePlain =>
            GetAnsiString(brows_ssh_KnownHost_get_name_plain);

        public KnownHostStatus Status =>
            brows_ssh_KnownHost_get_status(GetHandle());
    }
}
