using System;
using System.Linq;

namespace Brows.Cli {
    internal class CommandHelp : ICommandHelp {
        public CommandReflection Reflection { get; }

        public string HelpLine =>
            _HelpLine ?? (
            _HelpLine = string.Join(" ",
                Reflection.ArgumentProperties().OrderBy(p => !p.Required).Select(p => p.HelpToken).Concat(
                Reflection.SwitchProperties().OrderBy(p => !p.Required).Select(p => p.HelpToken))));
        private string _HelpLine;

        public CommandHelp(CommandReflection reflection) {
            Reflection = reflection ?? throw new ArgumentNullException(nameof(reflection));
        }
    }
}
