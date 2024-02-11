using Brows.Native;
using Domore.Logs;
using System;
using System.Runtime.InteropServices;

namespace Brows {
    internal sealed class BrowsNative : NativeLoader {
        private static readonly ILog Log = Logging.For(typeof(BrowsNative));

        [DllImport(Dll, CallingConvention = Call, CharSet = Chars)]
        private static extern int brows_init();

        [DllImport(Dll, CallingConvention = Call, CharSet = Chars)]
        private static extern void brows_logged(IntPtr handler);

        [DllImport(Dll, CallingConvention = Call, CharSet = Chars)]
        private static extern void brows_logging(IntPtr handler);

        [UnmanagedFunctionPointer(Call, CharSet = Chars)]
        private delegate int brows_logged_handler(int severity, IntPtr message);

        [UnmanagedFunctionPointer(Call, CharSet = Chars)]
        private delegate int brows_logging_handler(int severity);

        private readonly Delegate LoggedDelegate;
        private readonly Delegate LoggingDelegate;

        private readonly IntPtr LoggedFunctionPointer;
        private readonly IntPtr LoggingFunctionPointer;

        private int LoggedHandler(int severity, IntPtr message) {
            try {
                Log.Data((LogSeverity)severity, Marshal.PtrToStringAnsi(message));
            }
            catch (Exception ex) {
                Console.WriteLine(ex);
                return -1;
            }
            return 0;
        }

        private int LoggingHandler(int severity) {
            try {
                return Log.Enabled((LogSeverity)severity)
                    ? 1
                    : 0;
            }
            catch (Exception ex) {
                Console.WriteLine(ex);
                return 0;
            }
        }

        protected sealed override void HandleLoaded() {
            brows_logged(LoggedFunctionPointer);
            brows_logging(LoggingFunctionPointer);
            Try(brows_init);
        }

        public new const string Dll = "brows_native.dll";
        public const CallingConvention Call = CallingConvention.Cdecl;
        public const CharSet Chars = CharSet.Unicode;

        public BrowsNative() : base(Dll) {
            LoggedFunctionPointer = Marshal.GetFunctionPointerForDelegate(LoggedDelegate = new brows_logged_handler(LoggedHandler));
            LoggingFunctionPointer = Marshal.GetFunctionPointerForDelegate(LoggingDelegate = new brows_logging_handler(LoggingHandler));
        }
    }
}
