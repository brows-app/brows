using Brows.Exports;
using Brows.Git;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Brows;

internal static class GitFileSystemData {
    internal static readonly Definition[] Columns = [
        new GitRepoHead(), new GitTrackingBranch(), new GitRepoState(),
        new GitStatus(), new GitIndexStatus(), new GitWorkTreeStatus()
    ];

    public sealed class GitRepoHead : Definition {
        public GitRepoHead() : base("Git branch", 180) { }
        protected sealed override string Value(GitStatusView view, IFileSystemEntry entry) => view.Branch;
    }

    public sealed class GitTrackingBranch : Definition {
        public GitTrackingBranch() : base("Git tracking branch", 240) { }
        protected sealed override string Value(GitStatusView view, IFileSystemEntry entry) => view.Tracking;
    }

    public sealed class GitRepoState : Definition {
        public GitRepoState() : base("Git operation", 160) { }
        protected sealed override string Value(GitStatusView view, IFileSystemEntry entry) => view.Operation;
    }

    public sealed class GitStatus : Definition {
        public GitStatus() : base("Git status", 200) { }
        protected sealed override string Value(GitStatusView view, IFileSystemEntry entry) {
            var flags = Status(view, entry);
            if (flags == null) {
                return null;
            }
            if (flags == GitStatusFlags.Current) {
                return "Clean";
            }
            var index = GitStatusRow.Describe(flags.Value, index: true);
            var workTree = GitStatusRow.Describe(flags.Value, index: false);
            return index.Length == 0 ? workTree : workTree.Length == 0
                ? $"Staged: {index}" : $"Staged: {index}; {workTree}";
        }
    }

    public sealed class GitIndexStatus : Definition {
        public GitIndexStatus() : base("Git staged", 140) { }
        protected sealed override string Value(GitStatusView view, IFileSystemEntry entry) =>
            Status(view, entry) is GitStatusFlags status
                ? GitStatusRow.Describe(status, index: true)
                : null;
    }

    public sealed class GitWorkTreeStatus : Definition {
        public GitWorkTreeStatus() : base("Git working tree", 140) { }
        protected sealed override string Value(GitStatusView view, IFileSystemEntry entry) =>
            Status(view, entry) is GitStatusFlags status
                ? GitStatusRow.Describe(status, index: false)
                : null;
    }

    public abstract class Definition : EntryDataExport<IFileSystemEntry, string> {
        public sealed override string Group => "Git";
        public sealed override string Name { get; }

        protected Definition(string name, double width) {
            Name = name;
            Width = width;
        }

        protected static GitStatusFlags? Status(GitStatusView view, IFileSystemEntry entry) =>
            view.ForPath(entry.Path, entry.Kind == FileSystemEntryKind.Directory);

        protected abstract string Value(GitStatusView view, IFileSystemEntry entry);

        internal string Display(GitStatusView view, IFileSystemEntry entry) =>
            view is null ? null : Value(view, entry);

        protected sealed override async Task<string> GetValue(IFileSystemEntry entry,
                                                              Action<string> progress,
                                                              CancellationToken token) {
            if (token.IsCancellationRequested) {
                token.ThrowIfCancellationRequested();
            }
            if (entry == null) {
                return null;
            }
            var directory = entry.Kind == FileSystemEntryKind.Directory
                ? entry.Path
                : Path.GetDirectoryName(entry.Path);
            if (directory is null) {
                return null;
            }
            var view = await GitStatusReader.Read(directory, token).ConfigureAwait(false);
            return view is null ? null : Value(view, entry);
        }

        protected sealed override Task RefreshValue(IFileSystemEntry entry, CancellationToken token) {
            if (token.IsCancellationRequested) {
                token.ThrowIfCancellationRequested();
            }
            GitStatusReader.Invalidate();
            return Task.CompletedTask;
        }
    }
}
