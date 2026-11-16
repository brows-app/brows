using Brows.Git;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Brows;

internal sealed class GitStatusView {
    private static readonly StringComparer PathComparer = System.OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
    private readonly Dictionary<string, GitStatusFlags> FileFlags = new(PathComparer);
    private readonly Dictionary<string, GitStatusFlags> DirectoryFlags = new(PathComparer);

    public string Branch { get; }
    public string Tracking { get; }
    public string Summary { get; }
    public string Operation { get; }
    public string WorkDirectory { get; }
    public string GitDirectory { get; }
    public string HeadId { get; }
    public bool HasConflicts { get; }
    public bool IsBare { get; }
    public IReadOnlyList<GitStatusRow> Files { get; }

    internal GitStatusView(GitRepositoryStatus status) {
        ArgumentNullException.ThrowIfNull(status);
        WorkDirectory = status.WorkDirectory;
        GitDirectory = status.GitDirectory;
        HeadId = status.HeadId;
        IsBare = status.IsBare;
        Branch = status.IsDetached
            ? $"Detached HEAD · {status.HeadId?[..System.Math.Min(8, status.HeadId.Length)]}"
            : status.IsUnborn ? $"{status.Branch} · no commits" : status.Branch;
        Tracking = string.IsNullOrEmpty(status.Upstream)
            ? "No tracking branch"
            : status.UpstreamMissing ? $"{status.Upstream} · missing"
            : status.Ahead.HasValue && status.Behind.HasValue
                ? $"{status.Upstream} · {status.Ahead} ahead, {status.Behind} behind"
                : $"{status.Upstream} · comparison unavailable";
        Operation = status.State switch {
            GitRepoState.None => "",
            GitRepoState.Merge => "Merge in progress",
            GitRepoState.Revert or GitRepoState.RevertSequence => "Revert in progress",
            GitRepoState.Cherrypick or GitRepoState.CherrypickSequence => "Cherry-pick in progress",
            GitRepoState.Bisect => "Bisect in progress",
            GitRepoState.Rebase or GitRepoState.RebaseInteractive or GitRepoState.RebaseMerge => "Rebase in progress",
            GitRepoState.ApplyMailbox or GitRepoState.ApplyMailboxOrRebase => "Mailbox application in progress",
            _ => status.State.ToString()
        };
        Files = System.Array.AsReadOnly(status.Files
            .Select(file => new GitStatusRow(file))
            .OrderByDescending(file => file.IsConflict)
            .ThenBy(file => file.IsIgnored)
            .ThenBy(file => file.Path, StringComparer.Ordinal)
            .ToArray());
        foreach (GitStatusRow file in Files) {
            AddPath(file.Path, file.Status);
            if (!string.IsNullOrEmpty(file.OriginalPath)) {
                AddPath(file.OriginalPath, file.Status);
            }
        }
        var conflicts = Files.Count(file => file.IsConflict);
        var staged = Files.Count(file => !file.IsConflict && (file.Status & GitStatusRow.IndexFlags) != 0);
        var unstaged = Files.Count(file => !file.IsConflict && (file.Status & GitStatusRow.WorkTreeFlags) != 0);
        var untracked = Files.Count(file => (file.Status & GitStatusFlags.WorkTreeNew) != 0);
        HasConflicts = conflicts > 0;
        var summary = new List<string>();
        if (conflicts > 0) summary.Add($"{conflicts} conflicted");
        if (staged > 0) summary.Add($"{staged} staged");
        if (unstaged > 0) summary.Add($"{unstaged} unstaged");
        if (untracked > 0) summary.Add($"{untracked} untracked");
        Summary = IsBare ? "Bare repository" : summary.Count == 0 ? "Working tree clean" : string.Join(" · ", summary);
    }

    private void AddPath(string path, GitStatusFlags flags) {
        var key = path.TrimEnd('/');
        FileFlags[key] = FileFlags.GetValueOrDefault(key) | flags;
        if (path.EndsWith('/')) {
            DirectoryFlags[key] = DirectoryFlags.GetValueOrDefault(key) | flags;
        }
        for (var separator = key.LastIndexOf('/'); separator >= 0; separator = key.LastIndexOf('/')) {
            key = key[..separator];
            DirectoryFlags[key] = DirectoryFlags.GetValueOrDefault(key) | (flags & ~GitStatusFlags.Ignored);
        }
        DirectoryFlags["."] = DirectoryFlags.GetValueOrDefault(".") | (flags & ~GitStatusFlags.Ignored);
    }

    internal GitStatusFlags? ForPath(string path, bool directory) {
        if (string.IsNullOrEmpty(WorkDirectory) || IsBare) {
            return null;
        }
        var relative = Path.GetRelativePath(WorkDirectory, path).Replace('\\', '/');
        if (relative.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith(".git/", StringComparison.OrdinalIgnoreCase)) {
            return null;
        }
        if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative)) {
            return null;
        }
        GitStatusFlags result = FileFlags.GetValueOrDefault(relative);
        if (directory) {
            result |= DirectoryFlags.GetValueOrDefault(relative);
        }
        // Ignored directory entries are deliberately collapsed by the native scan.
        var parent = relative;
        for (var separator = parent.LastIndexOf('/'); separator >= 0; separator = parent.LastIndexOf('/')) {
            parent = parent[..separator];
            result |= FileFlags.GetValueOrDefault(parent) & GitStatusFlags.Ignored;
        }
        return result;
    }
}
