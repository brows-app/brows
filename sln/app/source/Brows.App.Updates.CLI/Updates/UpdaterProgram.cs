using System;
using System.Diagnostics;

namespace Brows.Updates;

internal static class UpdaterProgram {
    private static string ProgramArguments(string commandLine) {
        var line = commandLine.AsSpan().TrimStart();
        if (line.StartsWith("\"")) {
            var end = line[1..].IndexOf('"');
            return end < 0 ? "" : line[(end + 2)..].ToString();
        }
        var separator = line.IndexOfAny(' ', '\t');
        return separator < 0 ? "" : line[(separator + 1)..].ToString();
    }

    internal static int Run(string commandLine) {
        try {
            return UpdaterArguments.TryParse(ProgramArguments(commandLine), out var parsed) ? new UpdaterHandoff(parsed).Run() : 2;
        }
        catch (Exception ex) {
            Trace.TraceError(ex.ToString());
            return 1;
        }
    }
}
