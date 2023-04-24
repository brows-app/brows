using Brows.Config;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LOG = Domore.Logs.Log;

namespace Brows {
    internal sealed class ProgramMain {
        private WindowsProgram Default =>
            _Default ?? (
            _Default = new WindowsProgram());
        private WindowsProgram _Default;

        private IReadOnlyList<IProgram> Programs =>
            _Programs ?? (
            _Programs = Array.Empty<IProgram>());
        private IReadOnlyList<IProgram> _Programs;

        private ProgramCommand Command { get; }
        private ProgramConsole Console { get; }

        private ProgramMain(ProgramConsole console, ProgramCommand command) {
            Console = console ?? throw new ArgumentNullException(nameof(console));
            Command = command ?? throw new ArgumentNullException(nameof(command));
        }

        private async Task<ProgramConfig> Config(IProgram program, CancellationToken cancellationToken) {
            var file = Configure.File<ProgramConfig>();
            var conf = await file.Load(cancellationToken);
            return conf;
        }

        private bool ProgramNameValid(string s) {
            if (s?.Length > 0) {
                if (s.StartsWith('_') || char.IsLetter(s[0])) {
                    if (s.All(c => char.IsLetterOrDigit(c) || c == '_')) {
                        return true;
                    }
                }
            }
            return false;
        }

        private async Task<int> Run(CancellationToken token) {
            var context = new ProgramContext(Command, Console);
            var program = default(IProgram);
            var programName = Command.Name;
            var programNameValid = ProgramNameValid(programName);
            if (programNameValid) {
                program = Programs.FirstOrDefault(p => programName.Equals(p.Name, StringComparison.OrdinalIgnoreCase));
            }
            if (program == null) {
                var @default = program = await Default.Program(context, token);
                if (@default == null) {
                    return 0;
                }
            }
            var config = await Config(program, token);
            if (config.Console) {
                Console.Show();
            }
            return await program.Run(context, token);
        }

        private static async Task<int> Main(string[] args) {
            LOG.Conf.Configure(path: Path.Combine(
                await ConfigPath.FileReady(CancellationToken.None),
                "log.conf"));
            var command = new ProgramCommand(Environment.CommandLine, args);
            var console = new ProgramConsole();
            var program = new ProgramMain(console, command);
            using (console) {
                try {
                    return await program.Run(CancellationToken.None);
                }
                finally {
                    Logging.Complete();
                }
            }
        }
    }
}
