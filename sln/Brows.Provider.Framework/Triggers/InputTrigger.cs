using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Triggers {
    internal sealed class InputTrigger : IInputTrigger {
        public string Input { get; }
        public string Defined { get; }
        public IReadOnlySet<string> Aliases { get; }
        public IReadOnlySet<string> Options { get; }

        public InputTrigger(string defined, string input, params string[] aliases) {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            Defined = defined;
            Aliases = new HashSet<string>(aliases ?? [], StringComparer.CurrentCultureIgnoreCase);
            Options = new HashSet<string>(new[] { input }.Concat(Aliases), StringComparer.CurrentCultureIgnoreCase);
        }

        public bool Triggered(string s) {
            return Options.Contains(s);
        }

        public sealed override string ToString() {
            return Input;
        }
    }
}
