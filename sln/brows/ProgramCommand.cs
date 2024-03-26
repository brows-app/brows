using Domore.Conf.Cli;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    internal sealed class ProgramCommand : IProgramCommand {
        private string ProgramLine => _ProgramLine ??=
            GetProgramLine(CommandLine);
        private string _ProgramLine;

        private static string GetProgramLine(string commandLine) {
            if (commandLine == null) throw new ArgumentNullException(nameof(commandLine));
            if (commandLine.StartsWith("\"")) {
                for (var i = 1; i < commandLine.Length; i++) {
                    var c = commandLine[i];
                    if (c == '"') {
                        return commandLine.Substring(i).TrimStart('"');
                    }
                }
            }
            else {
                for (var i = 0; i < commandLine.Length; i++) {
                    var c = commandLine[i];
                    if (char.IsWhiteSpace(c)) {
                        return commandLine.Substring(i).TrimStart();
                    }
                }
            }
            return "";
        }

        public string Name =>
            _Name ?? (
            _Name = Args
                .TakeWhile(a => !a.StartsWith('-'))
                .FirstOrDefault() ?? "");
        private string _Name;

        public string CommandLine { get; }
        public IReadOnlyList<string> Args { get; }

        public ProgramCommand(string commandLine, IReadOnlyList<string> args) {
            CommandLine = commandLine;
            Args = args ?? throw new ArgumentNullException(nameof(args));
        }

        public T Configure<T>(T target) {
            return Cli.Configure(target, ProgramLine);
        }
    }
}
