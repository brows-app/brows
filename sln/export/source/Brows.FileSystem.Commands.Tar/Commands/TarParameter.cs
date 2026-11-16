using Domore.Conf;
using Domore.Conf.Cli;

namespace Brows.Commands;

internal sealed record TarParameter {
    [CliArgument]
    public string Output { get; init; }

    [Conf("force")]
    public TarDirection? Direction { get; init; }

    public bool? Overwrite { get; init; }
}
