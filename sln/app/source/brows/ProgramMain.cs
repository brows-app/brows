using Brows.Config;
using Brows.Programs;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Brows;

internal sealed class ProgramMain {
    private WindowsProgram ProgramDefault => field ??=
        new WindowsProgram();

    private IReadOnlyList<IProgram> Programs => field ??=
        Array.Empty<IProgram>();

    private ProgramCommand Command { get; }
    private ProgramConsole Console { get; }

    private ProgramMain(ProgramConsole console, ProgramCommand command) {
        Console = console ?? throw new ArgumentNullException(nameof(console));
        Command = command ?? throw new ArgumentNullException(nameof(command));
    }

    private static bool ProgramNameValid(string s) {
        if (s?.Length > 0) {
            if (s.StartsWith('_') || char.IsLetter(s[0])) {
                if (s.All(c => char.IsLetterOrDigit(c) || c == '_')) {
                    return true;
                }
            }
        }
        return false;
    }

    private Task<int> Run(CancellationToken token) {
        var context = new ProgramContext(Command, Console);
        var program = default(IProgram);
        var programName = Command.Name;
        var programNameValid = ProgramNameValid(programName);
        if (programNameValid) {
            program = Programs.FirstOrDefault(p => programName.Equals(p.Name, StringComparison.OrdinalIgnoreCase));
        }
        if (program == null) {
            program = ProgramDefault;
        }
        return program.Run(context, token);
    }

    private static async Task<int> Main(string[] args) {
        using (var programConsole = new ProgramConsole()) {
            var token = CancellationToken.None;
            var command = new ProgramCommand(Environment.CommandLine, args);
            var program = new ProgramMain(programConsole, command);
            var programConfig = await Configure.File<ProgramConfig>().Load(token);
            if (programConfig.Console) {
                programConsole.Show();
            }
            var logConfigPath = await ConfigPath.FileReady(token);
            var logConfigFile = Path.Combine(logConfigPath, "log.conf");
            Log.Conf.Configure(logConfigFile);
            try {
                return await program.Run(CancellationToken.None);
            }
            finally {
                Logging.Complete();
            }
        }
    }
}
