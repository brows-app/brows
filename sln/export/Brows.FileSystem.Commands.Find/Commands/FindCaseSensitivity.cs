using Domore.Conf.Cli;

namespace Brows.Commands {
    internal enum FindCaseSensitivity {
        [CliDisplay(false)]
        None = 0,
        Sensitive
    }
}
