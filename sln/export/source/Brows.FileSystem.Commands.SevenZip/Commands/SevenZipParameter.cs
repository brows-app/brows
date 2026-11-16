using Domore.Conf;
using Domore.Conf.Cli;

namespace Brows.Commands;

internal sealed record SevenZipParameter {
    [CliArgument]
    public string Output { get; init; }

    [Conf("force")]
    public SevenZipDirection? Direction { get; init; }

    [Conf("compression")]
    public int? CompressionLevel { get; init; }

    public bool? Overwrite { get; init; }
}
