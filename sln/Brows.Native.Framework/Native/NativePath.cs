using Domore.Logs;
using System;
using System.IO;
using System.Runtime.InteropServices;

using ENVIRONMENT = System.Environment;
using OPERATINGSYSTEM = System.OperatingSystem;

namespace Brows.Native {
    internal static class NativePath {
        private static readonly ILog Log = Logging.For(typeof(NativePath));

        private static string OperatingSystem =>
            _OperatingSystem ?? (
            _OperatingSystem =
                OPERATINGSYSTEM.IsLinux() ? "linux" :
                OPERATINGSYSTEM.IsMacOS() ? "macos" :
                OPERATINGSYSTEM.IsWindows() ? "windows" :
                throw new NotSupportedException());
        private static string _OperatingSystem;

        private static string Platform =>
            _Platform ?? (
            _Platform = ENVIRONMENT.Is64BitProcess ? "x64" : "x86");
        private static string _Platform;

        private static string Environment =>
            _Environment ?? (
            _Environment = $"{Platform}-{OperatingSystem}");
        private static string _Environment;

        public static string Root =>
            _Root ?? (
            _Root = Path.Combine(
                Path.GetDirectoryName(ENVIRONMENT.ProcessPath),
                "brows.native",
                Environment));
        private static string _Root;

        public static IntPtr Load(string dll) {
            if (Log.Info()) {
                Log.Info(
                    Log.Join(nameof(Load), dll),
                    Log.Join(nameof(Root), Root));
            }
            var path = Path.Combine(Root, dll);
            var hndl = NativeLibrary.Load(path);
            return hndl;
        }

        public static void Free(IntPtr handle) {
            NativeLibrary.Free(handle);
        }
    }
}
