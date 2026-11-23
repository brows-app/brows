namespace Brows.Composition;

/// <summary>
/// Supplies optional display choices for a file-system icon.
/// </summary>
public class FileSystemIconHint : IFileSystemIconHint {
    /// <summary>
    /// Gets or sets whether a directory icon appears open; null leaves the choice to the service.
    /// </summary>
    public bool? DirectoryOpen { get; set; }
}
