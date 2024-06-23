using Brows.Config;
using Brows.Diagnostics;
using Brows.Exports;
using Domore.Notification;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ENVIRONMENT = System.Environment;

namespace Brows {
    internal sealed class CommandProcess : Notifier {
        private IConfig<ProcessConfig> Config =>
            _Config ?? (
            _Config = Configure.File<ProcessConfig>());
        private IConfig<ProcessConfig> _Config;

        private async Task Start(string workingDirectory, IReadOnlyDictionary<string, string> environment, CancellationToken token) {
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
                    process.StartInfo.WorkingDirectory = workingDirectory ?? ENVIRONMENT.GetFolderPath(ENVIRONMENT.SpecialFolder.UserProfile);
                    process.StartInfo.Environment(environment);
                    await Task.Run(cancellationToken: token, function: process.Start);
                }
                finally {
                    await Task.Run(process.Dispose);
                }
            }
        }

        public IFixProcessStartInfoError Fix { get; set; }
        public IList<ProcessWrapper> List { get; } = new List<ProcessWrapper>();
        public IDictionary<string, string> Environment { get; set; }

        public async Task Start(string input, string workingDirectory, IReadOnlyDictionary<string, string> environment, CancellationToken token) {
            var env = Environment?.ToDictionary() ?? new();
            if (environment != null) {
                foreach (var variable in environment) {
                    env[variable.Key] = variable.Value;
                }
            }
            var i = input?.Trim() ?? "";
            if (i == "") {
                await Start(workingDirectory, env, token);
            }
            else {
                var process = new ProcessWrapper(i, workingDirectory, env, Fix);
                List.Insert(0, process);
                await process.Start(token);
            }
        }
    }
}
