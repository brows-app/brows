namespace Brows;

/// <summary>
/// Identifies how an update handoff ended.
/// </summary>
public enum AppUpdateOutcomeKind {
    /// <summary>
    /// The installer completed successfully.
    /// </summary>
    Installed,

    /// <summary>
    /// The installer completed and Windows needs to restart.
    /// </summary>
    RestartRequired,

    /// <summary>
    /// The handoff or installer encountered an error.
    /// </summary>
    Failed,

    /// <summary>
    /// Brows was still running after the shutdown timeout.
    /// </summary>
    NotStarted
}
