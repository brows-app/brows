using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    internal sealed class ProgramCommand : IProgramCommand {
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
