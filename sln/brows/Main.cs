using Domore.Runtime.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LOG = Domore.Logs.Log;

namespace Brows {
    using Composition.Hosting;
    using Config;

    internal static class Main {
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
            private ProgramConsole Console { get; }

            private Program(ProgramConsole console, ProgramCommand command) {
                Console = console ?? throw new ArgumentNullException(nameof(console));
                Command = command ?? throw new ArgumentNullException(nameof(command));
            }

            private async Task<ProgramConfig> Config(IProgram program, CancellationToken cancellationToken) {
                var dir = await ConfigPath.FileReady(cancellationToken);
                var log = Path.Combine(dir, "log.conf");
                LOG.Conf.Configure(log);

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
                var config = await Config(program, cancellationToken);
                if (config.Console) {
                    Console.Show();
                }
                return await program.Run(Command, Console, cancellationToken);
            }

            private static async Task<int> Main(string[] args) {
                var command = new ProgramCommand(Environment.CommandLine, args);
                var console = new ProgramConsole();
                var program = new Program(console, command);
                using (program.Console) {
                    return await program.Run(CancellationToken.None);
                }
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

        private class ProgramConfig {
            public bool Console { get; set; } = true;
        }

        private class ProgramConsole : IProgramConsole, IDisposable {
            private readonly object Locker = new();
            private bool Alloc;
            private bool Shown;

            public bool Show() {
                if (Shown == false) {
                    lock (Locker) {
                        if (Shown == false) {
                            Shown = true;
                            Alloc = kernel32.AttachConsole(-1)
                                ? false
                                : kernel32.AllocConsole();
                            return true;
                        }
                    }
                }
                return false;
            }

            public bool Hide() {
                if (Shown) {
                    lock (Locker) {
                        if (Shown) {
                            Shown = false;
                            if (Alloc) {
                                Alloc = false;
                                try {
                                    kernel32.FreeConsole();
                                }
                                catch {
                                }
                            }
                            return true;
                        }
                    }
                }
                return false;
            }

            void IDisposable.Dispose() {
                if (Alloc) {
                    try {
                        kernel32.FreeConsole();
                    }
                    catch {
                    }
                }
            }
        }
    }
}
