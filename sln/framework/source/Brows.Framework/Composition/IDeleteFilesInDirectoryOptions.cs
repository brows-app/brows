namespace Brows.Composition;

/// <summary>
/// Specifies whether deleted files may be recovered.
/// </summary>
public interface IDeleteFilesInDirectoryOptions {
    /// <summary>
    /// Gets whether deletion should bypass recovery; null uses the service default.
    /// </summary>
    bool? Unrecoverable { get; }
}
