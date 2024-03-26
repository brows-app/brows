using Brows.Native;
using System.Runtime.InteropServices;

using brows_ERROR = System.Int32;

namespace Brows {
    internal sealed class Win32Native : NativeLoader {
        [DllImport(Dll, CallingConvention = Call)] private static extern brows_ERROR brows_runtime_win32_init();
        [DllImport(Dll, CallingConvention = Call)] private static extern brows_ERROR brows_runtime_win32_exit();

        protected sealed override void HandleLoaded() {
            Try(brows_runtime_win32_init);
        }

        protected sealed override void HandleFreeing() {
            Try(brows_runtime_win32_exit);
        }

        public const CallingConvention Call = CallingConvention.Cdecl;
        public new const string Dll = "brows_runtime_win32.dll";

        public Win32Native() : base(Dll) {
        }
    }
}
