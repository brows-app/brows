namespace Brows.Composition;

/// <summary>
/// Supplies optional display choices for a file-system icon.
/// </summary>
public interface IFileSystemIconHint {
    /// <summary>
    /// Gets whether a directory icon appears open; null leaves the choice to the service.
    /// </summary>
    bool? DirectoryOpen { get; }
}
