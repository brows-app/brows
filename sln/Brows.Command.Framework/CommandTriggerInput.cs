using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    internal sealed class CommandTriggerInput : ITriggerInput {
        private const StringComparison Comparison = StringComparison.CurrentCultureIgnoreCase;

        private CommandTriggerInput(string @string, IReadOnlySet<string> aliases, IReadOnlySet<string> options) {
            String = @string ?? throw new ArgumentNullException(nameof(@string));
            Aliases = aliases ?? throw new ArgumentNullException(nameof(aliases));
            Options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public string String { get; }
        public IReadOnlySet<string> Aliases { get; }
        public IReadOnlySet<string> Options { get; }

        public CommandTriggerInput(string @string, params string[] aliases) {
            String = @string ?? throw new ArgumentNullException(nameof(@string));
            Aliases = new HashSet<string>(aliases ?? Array.Empty<string>(), StringComparer.CurrentCultureIgnoreCase);
            Options = new HashSet<string>(new[] { @string }.Concat(Aliases), StringComparer.CurrentCultureIgnoreCase);
        }

        public bool Triggered(string s) {
            return Options.Contains(s);
        }

        public sealed override bool Equals(object obj) {
            return
                obj is CommandTriggerInput other &&
                other.String.Equals(String);
        }

        public sealed override int GetHashCode() {
            return String.GetHashCode();
        }

        public sealed override string ToString() {
            return String;
        }
    }
}
