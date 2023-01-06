using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Composition.Hosting;

    internal static class Main {
        private static readonly ILog Log = Logging.For(typeof(Main));

        private class Program {
            private IProgram Default =>
                _Default ?? (
                _Default = new WindowsProgram());
            private IProgram _Default;

            private Composer Composer =>
                _Composer ?? (
                _Composer = new Composer());
            private Composer _Composer;

            private IReadOnlyList<IProgram> Programs =>
                _Programs ?? (
                _Programs = Composer.Programs());
            private IReadOnlyList<IProgram> _Programs;

            private ProgramCommand Command { get; }

            private Program(ProgramCommand command) {
                Command = command ?? throw new ArgumentNullException(nameof(command));
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

            private async Task<int> Run(CancellationToken cancellationToken) {
                var program = default(IProgram);
                var programName = Command.Name;
                var programNameValid = ProgramNameValid(programName);
                if (programNameValid) {
                    program = Programs.FirstOrDefault(p => programName.Equals(p.Name, StringComparison.OrdinalIgnoreCase));
                }
                if (program == null) {
                    program = Default;
                }
                return await program.Run(Command, cancellationToken);
            }

            private static async Task<int> Main(string[] args) {
                var command = new ProgramCommand(Environment.CommandLine, args);
                var program = new Program(command);
                return await program.Run(CancellationToken.None);
            }
        }

        private class ProgramCommand : IProgramCommand {
            public string Name =>
                _Name ?? (
                _Name = Args
                    .TakeWhile(a => !a.StartsWith('-'))
                    .FirstOrDefault() ?? "");
            private string _Name;

            public string Line { get; }
            public IReadOnlyList<string> Args { get; }

            public ProgramCommand(string line, IReadOnlyList<string> args) {
                Line = line;
                Args = args ?? throw new ArgumentNullException(nameof(args));
            }
        }
    }
}
