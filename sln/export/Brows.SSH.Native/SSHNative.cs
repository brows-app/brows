using Brows.Native;
using System;
using System.Runtime.InteropServices;

namespace Brows {
    public sealed class SSHNative : NativeLoader {
        [DllImport(Dll, CallingConvention = Call)] private static extern int brows_ssh_init();
        [DllImport(Dll, CallingConvention = Call)] private static extern int brows_ssh_exit();
        [DllImport(Dll, CallingConvention = Call)] private static extern IntPtr brows_ssh_name(int error);

        protected sealed override void HandleLoaded() {
            Try(brows_ssh_init);
        }

        protected sealed override void HandleFreeing() {
            Try(brows_ssh_exit);
        }

        public new const string Dll = "brows_ssh.dll";
        public const CallingConvention Call = CallingConvention.Cdecl;

        public SSHNative() : base(Dll) {
        }

        public static string Name(int error) {
            var p = brows_ssh_name(error);
            return p == IntPtr.Zero
                ? null
                : Marshal.PtrToStringAnsi(p);
        }
    }
}
