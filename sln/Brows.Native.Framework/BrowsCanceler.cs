using System.Runtime.InteropServices;
using System.Threading;

namespace Brows {
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct BrowsCanceler {
        [UnmanagedFunctionPointer(BrowsNative.Call, CharSet = BrowsNative.Chars)]
        private delegate int CanceledDelegate(ref BrowsCanceler p);

        private readonly CanceledDelegate Canceled;

        public static readonly BrowsCanceler None = new();

        private BrowsCanceler(CanceledDelegate canceled) {
            Canceled = canceled;
        }

        public static BrowsCanceler From(CancellationToken token) {
            int canceled(ref BrowsCanceler p) {
                return token.IsCancellationRequested
                    ? 1
                    : 0;
            }
            return new BrowsCanceler(canceled);
        }
    }
}
