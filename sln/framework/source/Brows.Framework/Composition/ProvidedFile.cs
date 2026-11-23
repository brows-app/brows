namespace Brows.Composition;

/// <summary>
/// Pairs an original file path with the relative path to use at its destination.
/// </summary>
public sealed record ProvidedFile {
    /// <summary>
    /// Gets the original file-system path.
    /// </summary>
    public string OriginalPath { get; }

    /// <summary>
    /// Gets the path to use relative to the destination.
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// Initializes a file path and its relative destination path.
    /// </summary>
    /// <param name="originalPath">
    /// The original file-system path.
    /// </param>
    /// <param name="relativePath">
    /// The path relative to the destination.
    /// </param>
    public ProvidedFile(string originalPath, string relativePath) {
        OriginalPath = originalPath;
        RelativePath = relativePath;
    }
}
