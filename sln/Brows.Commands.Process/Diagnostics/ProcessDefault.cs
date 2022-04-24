using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Diagnostics {
    using Threading.Tasks;

    internal class ProcessDefault {
        public async Task Start(string workingDirectory, CancellationToken cancellationToken) {
            var process = new Process();
            try {
                process.StartInfo.Arguments = $"/k";
                process.StartInfo.CreateNoWindow = false;
                process.StartInfo.ErrorDialog = false;
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.WorkingDirectory = workingDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                await Async.Run(cancellationToken, process.Start);
            }
            finally {
                await Async.Run(CancellationToken.None, process.Dispose);
            }
        }
    }
}
