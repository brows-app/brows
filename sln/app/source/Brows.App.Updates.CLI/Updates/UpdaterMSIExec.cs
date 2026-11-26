using System;
using System.Diagnostics;
using System.IO;

namespace Brows.Updates;

internal static class UpdaterMSIExec {
    internal static ProcessStartInfo StartInfo(string msi, string log) {
        return new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "msiexec.exe")) {
            UseShellExecute = false,
            ArgumentList = { "/i", msi, "/passive", "/norestart", "/l*v", log }
        };
    }
}
