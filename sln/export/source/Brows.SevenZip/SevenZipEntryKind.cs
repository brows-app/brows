namespace Brows;

/// <summary>
/// Identifies whether an item stored in a 7-Zip archive is a file or directory.
/// </summary>
public enum SevenZipEntryKind {
    /// <summary>
    /// The archive entry represents a file.
    /// </summary>
    File,

    /// <summary>
    /// The archive entry represents a directory.
    /// </summary>
    Path
}
