using Domore.Conf.Cli;

namespace Brows.Commands {
    internal sealed class SelectParameter {
        [CliArgument]
        public string Pattern { get; set; }
        public bool Add { get; set; }
        public bool All { get; set; }
        public bool CaseSensitive { get; set; }
    }
}
