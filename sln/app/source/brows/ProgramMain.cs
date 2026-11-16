using Brows.Programs;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Brows;

internal sealed class ProgramMain {
    private WindowsProgram ProgramDefault => field ??=
        new WindowsProgram();

    private IReadOnlyList<IProgram> Programs => field ??=
        Array.Empty<IProgram>();

    private ProgramCommand Command { get; }

    private ProgramMain(ProgramCommand command) {
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
        var context = new ProgramContext(Command);
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
        var token = CancellationToken.None;
        var command = new ProgramCommand(Environment.CommandLine, args);
        var program = new ProgramMain(command);
        await ProgramLogging.Configure(token);
        try {
            return await program.Run(CancellationToken.None);
        }
        finally {
            Logging.Complete();
        }
    }
}
