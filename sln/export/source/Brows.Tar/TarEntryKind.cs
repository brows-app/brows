namespace Brows;

/// <summary>
/// Identifies whether an item stored in a tar archive is a file or directory.
/// </summary>
public enum TarEntryKind {
    /// <summary>
    /// The archive entry represents a file.
    /// </summary>
    File,

    /// <summary>
    /// The archive entry represents a directory.
    /// </summary>
    Path
}
