using System;

namespace Brows;

/// <summary>
/// Describes an installer asset and its published verification data.
/// </summary>
public sealed record AppUpdatePackage {
    /// <summary>
    /// Gets the GitHub download URL.
    /// </summary>
    public required Uri Url { get; init; }

    /// <summary>
    /// Gets the installer filename.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the SHA-256 digest as 64 lowercase hexadecimal characters.
    /// </summary>
    public required string Sha256 { get; init; }

    /// <summary>
    /// Gets the expected size in bytes.
    /// </summary>
    public required long Size { get; init; }
}
