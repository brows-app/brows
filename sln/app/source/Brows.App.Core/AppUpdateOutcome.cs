namespace Brows;

/// <summary>
/// Describes the result of the previous update handoff.
/// </summary>
public sealed record AppUpdateOutcome {
    /// <summary>
    /// Gets the version the handoff tried to install.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets the result of the handoff.
    /// </summary>
    public required AppUpdateOutcomeKind Kind { get; init; }

    /// <summary>
    /// Gets the Windows Installer exit code, or -1 if the handoff encountered an exception.
    /// A handoff that timed out waiting for Brows to exit records 0.
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// Gets the full path to the installer log.
    /// </summary>
    public string Log { get; init; }
}
