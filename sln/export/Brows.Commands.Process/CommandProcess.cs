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

        private async Task Start(string workingDirectory, CancellationToken token) {
            var config = await Config.Load(token).ConfigureAwait(false);
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
                    await Task.Run(cancellationToken: token, function: process.Start).ConfigureAwait(false);
                }
                finally {
                    await Task.Run(process.Dispose).ConfigureAwait(false);
                }
            }
        }

        public IFixProcessStartInfoError Fix { get; set; }
        public IList<ProcessWrapper> List { get; } = new List<ProcessWrapper>();
        public IDictionary<string, string> Environment { get; set; }

        public Task Start(string input, string workingDirectory, IReadOnlyDictionary<string, string> environment, CancellationToken token) {
            var i = input?.Trim() ?? "";
            if (i == "") {
                return Start(workingDirectory, token);
            }
            var env = Environment?.ToDictionary() ?? [];
            if (environment != null) {
                foreach (var variable in environment) {
                    env[variable.Key] = variable.Value;
                }
            }
            var process = new ProcessWrapper(i, workingDirectory, env, Fix);
            List.Insert(0, process);
            return process.Start(token);
        }
    }
}
