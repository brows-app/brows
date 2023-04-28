using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class FixProcessStartInfoError : IFixProcessStartInfoError {
        public async Task<bool> Work(ProcessStartInfo startInfo, Exception error, CancellationToken token) {
            if (startInfo == null) {
                return false;
            }
            if (error is Win32Exception win32) {
                if (win32.NativeErrorCode == 2) {
                    var filename = startInfo.FileName;
                    if (filename.Contains(Path.DirectorySeparatorChar) == false && filename.Contains(Path.AltDirectorySeparatorChar) == false) {
                        startInfo.Arguments = $"/C {filename} {startInfo.Arguments}";
                        startInfo.FileName = "cmd.exe";
                        return true;
                    }
                }
            }
            return await Task.FromResult(false);
        }
    }
}
