using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class FixProcessError : IFixProcessError {
        public async Task<bool> Work(ProcessStartInfo startInfo, Exception error, CancellationToken token) {
            if (startInfo == null) {
                return false;
            }
            if (error is Win32Exception win32) {
                if (win32.NativeErrorCode == 2) {
                    startInfo.Arguments = $"/C {startInfo.FileName} {startInfo.Arguments}";
                    startInfo.FileName = "cmd.exe";
                    return true;
                }
            }
            await Task.CompletedTask;
            return false;
        }
    }
}
