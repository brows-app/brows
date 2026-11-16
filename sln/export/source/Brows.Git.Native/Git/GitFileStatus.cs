namespace Brows.Git;

internal sealed class GitFileStatus {
    public string Path { get; }
    public string OriginalPath { get; }
    public GitStatusFlags Status { get; }

    internal GitFileStatus(string path, string originalPath, GitStatusFlags status) {
        Path = path;
        OriginalPath = originalPath;
        Status = status;
    }
}
