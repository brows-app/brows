using Domore.Logs;

namespace Brows.CLI;

/// <summary>
/// Information about the log file of a CLI program.
/// </summary>
public sealed class CliLogFile {
    /// <summary>
    /// Gets or sets the target directory for log files.
    /// </summary>
    public string Directory { get; init; }

    /// <summary>
    /// Gets or sets the name of the log file.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets or sets the log threshold at which logs are written to the file.
    /// </summary>
    public LogSeverity? Threshold { get; init; }
}
