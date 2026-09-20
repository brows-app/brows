using Brows.Composition;
using Domore.Conf.Cli;

namespace Brows.Commands;

internal sealed class CommandCliProvider : IExport {
    public CliProvider CliProvider => field ??=
        new(new CliSetup());
}
