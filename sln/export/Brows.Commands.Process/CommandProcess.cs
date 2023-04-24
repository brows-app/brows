using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Config;
    using Diagnostics;

    internal class CommandProcess : Notifier {
        private IConfig<ProcessConfig> Config =>
            _Config ?? (
            _Config = Configure.File<ProcessConfig>());
        private IConfig<ProcessConfig> _Config;

        private async Task Start(string workingDirectory, CancellationToken token) {
            var config = await Config.Load(token);
            var procDef = config?.Default;
            var procFile = procDef?.FileName?.Trim() ?? "";
            if (procFile != "") {
                var process = new Process();
                try {
                    process.StartInfo.Arguments = procDef.Arguments;
                    process.StartInfo.CreateNoWindow = false;
                    process.StartInfo.ErrorDialog = false;
                    process.StartInfo.FileName = procFile;
                    process.StartInfo.UseShellExecute = true;
                    process.StartInfo.WorkingDirectory = workingDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    await Task.Run(cancellationToken: token, function: process.Start);
                }
                finally {
                    await Task.Run(process.Dispose);
                }
            }
        }

        public IList<ProcessWrapper> List { get; } = new List<ProcessWrapper>();

        public async Task Start(string input, string workingDirectory, CancellationToken token) {
            var i = input?.Trim() ?? "";
            if (i == "") {
                await Start(workingDirectory, token);
            }
            else {
                var process = new ProcessWrapper(i, workingDirectory);
                List.Insert(0, process);
                await process.Start(token);
            }
        }
    }
}
