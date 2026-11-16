using Brows.Diagnostics;
using Brows.Diagnostics.Windows;
using Domore.Logs;
using Domore.Notification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ENVIRONMENT = System.Environment;
using Process = System.Diagnostics.Process;

namespace Brows;

internal sealed class CommandProcess : Notifier {
    private static readonly ILog Log = Logging.For(typeof(CommandProcess));

    private IConfiguration<ProcessConfig> Config => field ??=
        ConfigFile.File<ProcessConfig>();

    private async Task Start(string workingDirectory, CancellationToken token) {
        var config = await Config.Load(token);
        var procDef = config?.Default;
        var procFile = procDef?.FileName?.Trim() ?? "";
        if (procFile != "") {
            using (var process = new Process()) {
                process.StartInfo.Arguments = procDef.Arguments;
                process.StartInfo.CreateNoWindow = false;
                process.StartInfo.ErrorDialog = false;
                process.StartInfo.FileName = procFile;
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.WorkingDirectory = workingDirectory ??
                    ENVIRONMENT.GetFolderPath(ENVIRONMENT.SpecialFolder.UserProfile);
                process.Start();
            }
        }
    }

    internal IConfigurationFile ConfigFile { get; set; }

    public IList<IProcessProxy> List { get; } = [];
    public IDictionary<string, string> Environment { get; set; }

    public Task Start(string input,
                      string workingDirectory,
                      IReadOnlyDictionary<string, string> environment,
                      CancellationToken token) {
        var i = input?.Trim() ?? "";
        if (i == "") {
            return Start(workingDirectory, token);
        }
        var env = Environment?.ToDictionary() ?? [];
        if (environment is not null) {
            foreach (var variable in environment) {
                env[variable.Key] = variable.Value;
            }
        }
        var inpParts = i.Split(Array.Empty<char>(), 2, StringSplitOptions.None);
        var fileName = inpParts[0];
        var arguments = inpParts.Length > 1 ? inpParts[1] : "";
        var processor = new ProcessAgent {
            Arguments = arguments,
            Environment = env,
            FileName = fileName,
            OnErrorCaught = ex => {
                if (Log.Warn()) {
                    Log.Warn(ex);
                }
            },
            OnProxyCreated = proxy => {
                List.Insert(0, proxy);
            },
            StartInfoFixer = new WindowsCmdFixer(),
            WorkingDirectory = workingDirectory,
        };
        return processor.Start(token);
    }
}
