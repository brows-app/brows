using Domore.Logs;

namespace Brows.CLI;

/// <summary>
/// Information about the console log of a CLI program.
/// </summary>
public sealed class CliLogConsole {
    /// <summary>
    /// Gets or sets the threshold at which logs are displayed in the console.
    /// </summary>
    public LogSeverity Threshold { get; init; } = LogSeverity.Info;
}
