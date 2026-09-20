using System.Collections.Generic;
using System.Linq;

namespace Brows.CLI;

public sealed class CliProgramData {
    private static IReadOnlyList<string> Lines(string data) {
        var s = data?.Trim() ?? "";
        if (s == "") {
            return [];
        }
        return [.. s
            .Split('\n')
            .Select(line => line.Trim())];
    }

    /// <summary>
    /// Gets the lines of output data.
    /// </summary>
    public IReadOnlyList<string> OutputLines => field ??= Lines(Output);

    /// <summary>
    /// Gets the lines of log data.
    /// </summary>
    public IReadOnlyList<string> LogLines => field ??= Lines(Log);

    /// <summary>
    /// Gets the output string.
    /// </summary>
    public string Output { get; }

    /// <summary>
    /// Gets the log string.
    /// </summary>
    public string Log { get; }

    /// <summary>
    /// Gets a flag that indicates whether or not the program was canceled.
    /// </summary>
    public bool Canceled { get; }

    /// <summary>
    /// Creates new data.
    /// </summary>
    /// <param name="canceled">A flag that indicates whether or not the program was canceled.</param>
    /// <param name="output">The output string.</param>
    /// <param name="log">The log string.</param>
    public CliProgramData(bool canceled, string output, string log) {
        Canceled = canceled;
        Output = output;
        Log = log;
    }
}
