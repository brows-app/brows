namespace Brows.Git;

internal sealed class GitException : Exception {
    public int Error { get; }

    public GitException(int error, string message) : base(
        string.IsNullOrWhiteSpace(message)
            ? $"Git operation failed with error {error}."
            : $"Git operation failed with error {error}: {message}") {
        Error = error;
    }
}
