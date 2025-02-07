using Brows.Native;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public sealed class UrlNative : NativeLoader {
        [DllImport(Dll, CallingConvention = Call)] private static extern int brows_url_init();
        [DllImport(Dll, CallingConvention = Call)] private static extern int brows_url_exit();

        private UrlNative() : base(Dll) {
        }

        protected sealed override void HandleLoaded() {
            Try(brows_url_init);
        }

        protected sealed override void HandleFreeing() {
            Try(brows_url_exit);
        }

        public new const string Dll = "brows_url.dll";
        public const CallingConvention Call = CallingConvention.Cdecl;

        public static Task<bool> Load(CancellationToken token) {
            return new UrlNative().Loaded(token);
        }
    }
}
