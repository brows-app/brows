namespace Brows.Composition;

public sealed record ProvidedFile {
    public string OriginalPath { get; }
    public string RelativePath { get; }

    public ProvidedFile(string originalPath, string relativePath) {
        OriginalPath = originalPath;
        RelativePath = relativePath;
    }
}
