using Domore.Threading;

namespace Brows {
    internal static class Win32ThreadPool {
        public static readonly STAThreadPool Common = new STAThreadPool(nameof(Win32ThreadPool));
    }
}
