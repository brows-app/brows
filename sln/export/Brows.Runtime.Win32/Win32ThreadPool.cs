using Domore.Threading;

namespace Brows {
    internal static class Win32ThreadPool {
        public static readonly STAThreadPool Common = new(nameof(Win32ThreadPool));
    }
}
