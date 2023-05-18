using Domore.Conf.Cli;

namespace Brows.Commands {
    internal sealed class RenameParameter {
        [CliArgument]
        public string Pattern { get; set; }
    }
}
