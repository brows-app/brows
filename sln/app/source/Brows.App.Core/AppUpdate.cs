using System;

namespace Brows;

/// <summary>
/// Describes a newer Brows release and its installer, when available.
/// </summary>
public sealed record AppUpdate {
    /// <summary>
    /// Gets the release version without the tag prefix.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets the release page.
    /// </summary>
    public required Uri Page { get; init; }

    /// <summary>
    /// Gets the time at which the release was published.
    /// </summary>
    public DateTimeOffset Published { get; init; }

    /// <summary>
    /// Gets whether this is a pre-release.
    /// </summary>
    public bool PreRelease { get; init; }

    /// <summary>
    /// Gets the requested architecture, or null if none applies.
    /// </summary>
    public string Architecture { get; init; }

    /// <summary>
    /// Gets the verifiable installer for the requested architecture, or null if none exists.
    /// </summary>
    public AppUpdatePackage Package { get; init; }
}
