using System;
using System.IO;
using System.Threading.Tasks;

namespace Brows;

internal sealed class GitStatusProviderFactory : ProviderFactory<GitStatusProvider> {
    private const string Prefix = "git-status:";

    internal static string IDFor(string path) =>
        Prefix + new Uri(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path))).AbsoluteUri;

    internal static bool TryPath(string id, out string path) {
        path = null;
        if (id == null || !id.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ||
            !Uri.TryCreate(id[Prefix.Length..], UriKind.Absolute, out Uri uri) ||
            !uri.IsFile || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            !Path.IsPathFullyQualified(uri.LocalPath)) {
            return false;
        }
        path = uri.LocalPath;
        return true;
    }

    internal static async Task<GitStatusProvider> Open(string path, CancellationToken token) {
        var status = await GitStatusReader.Read(path, token);
        if (status == null) {
            return null;
        }
        var root = status.IsBare
            ? status.GitDirectory
            : status.WorkDirectory;
        return new GitStatusProvider(Path.TrimEndingDirectorySeparator(root), status);
    }

    protected sealed override Task<GitStatusProvider> CreateFor(string id, IPanel panel, CancellationToken token) {
        if (token.IsCancellationRequested) {
            token.ThrowIfCancellationRequested();
        }
        return TryPath(id, out string path)
            ? Open(path, token)
            : Task.FromResult<GitStatusProvider>(null);
    }
}
