using Brows.Updates;
using System.Runtime.InteropServices;

namespace Brows;

internal static class Program {
    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern nint GetCommandLineW();

    internal static int Main() {
        return UpdaterProgram.Run(Marshal.PtrToStringUni(GetCommandLineW()));
    }
}
