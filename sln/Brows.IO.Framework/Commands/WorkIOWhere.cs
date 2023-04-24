using Domore.Conf;
using Domore.Conf.Cli;

namespace Brows.Commands {
    public enum WorkIOWhere {
        [CliDisplay(false)]
        None = 0,

        [Conf("a", "auto")]
        [CliDisplay(true)]
        [CliDisplayOverride("(a)uto")]
        Auto,

        [Conf("h", "here")]
        [CliDisplay(true)]
        [CliDisplayOverride("(h)ere")]
        Active,

        [Conf("n", "next")]
        [CliDisplay(true)]
        [CliDisplayOverride("(n)ext")]
        Next,

        [Conf("p", "previous")]
        [CliDisplay(true)]
        [CliDisplayOverride("(p)revious")]
        Previous
    }
}
