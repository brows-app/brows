namespace Brows;

/// <summary>
/// Selects a release to look up.
/// </summary>
public sealed record AppRequest {
    /// <summary>
    /// Gets whether to include pre-releases when no version is pinned.
    /// </summary>
    public bool AllowPreRelease { get; init; }

    /// <summary>
    /// Gets the installer architecture, or null to use the running process architecture.
    /// </summary>
    public string Architecture { get; init; }

    /// <summary>
    /// Gets the exact version to find, or null to select the newest release.
    /// </summary>
    public string Version { get; init; }
}
