namespace Brows.IO;

/// <summary>
/// Represents configuration options for file system operations, including error handling and resiliency settings.
/// </summary>
/// <remarks>
/// This class provides properties to control the behavior of file system operations, such as whether
/// exceptions  should be thrown for specific error conditions and how resiliency is handled in the event of transient
/// failures.
/// </remarks>
public sealed record FileSysArg {
    /// <summary>
    /// Gets or sets a value indicating whether an exception should be thrown when an item is not found.
    /// </summary>
    public bool ThrowOnNotFound { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether an exception should be thrown when unauthorized access is encountered.
    /// </summary>
    /// <remarks>
    /// When set to <see langword="true"/>, unauthorized access attempts will result in an exception
    /// being thrown. If set to <see langword="false"/>, the operation will fail silently or handle the unauthorized
    /// access without throwing.
    /// </remarks>
    public bool ThrowOnUnauthorizedAccess { get; init; } = true;

    /// <summary>
    /// Gets or sets the resiliency level, which represents the system's ability to recover from failures or
    /// disruptions.
    /// </summary>
    public int Resiliency { get; init; }

    /// <summary>
    /// Gets or sets the delay, in milliseconds, applied to retry operations to improve resiliency.
    /// </summary>
    public int ResiliencyDelay { get; init; } = 100;

    /// <summary>
    /// Gets or sets an object that may catch exceptions thrown during file-system interaction.
    /// </summary>
    public IFileSysErrorHandler ErrorHandler { get; init; }
}
