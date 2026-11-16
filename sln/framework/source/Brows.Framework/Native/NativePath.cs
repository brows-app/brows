using Domore.Logs;
using System;
using System.IO;
using System.Runtime.InteropServices;
using ENVIRONMENT = System.Environment;
using OPERATINGSYSTEM = System.OperatingSystem;

namespace Brows.Native;

internal static class NativePath {
    private static readonly ILog Log = Logging.For(typeof(NativePath));

    private static string OperatingSystem => field ??=
        OPERATINGSYSTEM.IsLinux() ? "linux" :
        OPERATINGSYSTEM.IsMacOS() ? "macos" :
        OPERATINGSYSTEM.IsWindows() ? "windows" :
        throw new NotSupportedException("Unsupported operating system.");

    private static string Platform => field ??=
        (RuntimeInformation.ProcessArchitecture switch {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            var architecture => throw new NotSupportedException($"Unsupported architecture: {architecture}")
        });

    private static string Environment => field ??=
        $"{Platform}-{OperatingSystem}";

    public static string Root => field ??=
        Path.Combine(
            Path.GetDirectoryName(ENVIRONMENT.ProcessPath),
            "brows.native",
            Environment);

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
