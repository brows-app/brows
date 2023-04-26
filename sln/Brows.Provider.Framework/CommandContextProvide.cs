using Domore.Conf;
using Domore.Conf.Cli;

namespace Brows {
    public enum CommandContextProvide {
        [Conf("h", "here")]
        [CliDisplayOverride("(h)ere")]
        ActivePanel,

        [Conf("p", "panel")]
        [CliDisplayOverride("(p)anel")]
        AddPanel,

        [Conf("w", "window")]
        [CliDisplayOverride("(w)indow")]
        AddCommander
    }
}
