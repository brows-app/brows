using Domore.Conf.Cli;

namespace Brows.Commands;

internal sealed class LogsParameter {
    [CliArgument]
    public CommandContextProvide? Where { get; set; }
}
