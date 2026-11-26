using Domore.Conf.Cli;
using System.IO;

namespace Brows.Updates;

internal sealed record UpdaterArguments {
    private static readonly CliProvider Cli = new(new CliSetup());

    private UpdaterArguments(Args args, AppVersion version) {
        Pid = args.Pid;
        Msi = args.Msi;
        Log = args.Log;
        Outcome = args.Outcome;
        Version = version;
        Updates = args.Updates;
        Launch = args.Launch;
    }

    private static bool ValidPath(string path) {
        return path is not null &&
               Path.IsPathFullyQualified(path) &&
               path.IndexOf('\0') < 0;
    }

    internal int Pid { get; }
    internal string Msi { get; }
    internal string Log { get; }
    internal string Outcome { get; }
    internal AppVersion Version { get; }
    internal string Updates { get; }
    internal string Launch { get; }

    internal static bool TryParse(string commandLine, out UpdaterArguments result) {
        Args args;
        try {
            args = Cli.Configure(new Args(), commandLine);
        }
        catch (CliException) {
            result = null;
            return false;
        }
        if (args.Pid <= 0) {
            result = null;
            return false;
        }
        var versionValid = AppVersion.TryParse(args.Version, out var version) && version.Text == args.Version;
        if (!versionValid) {
            result = null;
            return false;
        }
        var pathsValid = ValidPath(args.Msi) &&
                         ValidPath(args.Log) &&
                         ValidPath(args.Outcome) &&
                         ValidPath(args.Updates) &&
                         ValidPath(args.Launch);
        if (!pathsValid) {
            result = null;
            return false;
        }
        result = new(args, version);
        return true;
    }

    private sealed class Args {
        [CliRequired]
        public int Pid { get; init; }

        [CliRequired]
        public string Msi { get; init; }

        [CliRequired]
        public string Log { get; init; }

        [CliRequired]
        public string Outcome { get; init; }

        [CliRequired]
        public string Version { get; init; }

        [CliRequired]
        public string Updates { get; init; }

        [CliRequired]
        public string Launch { get; init; }
    }
}
