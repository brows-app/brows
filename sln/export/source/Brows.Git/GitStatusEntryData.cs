using System;
using System.Threading.Tasks;

namespace Brows;

internal static class GitStatusEntryData {
    public abstract class Text : EntryDataExport<GitStatusEntry, string> {
        protected Text(string name, double width) {
            Name = name;
            Width = width;
        }

        protected abstract string Value(GitStatusRow row);

        protected sealed override Task<string> GetValue(GitStatusEntry entry, Action<string> progress, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(Value(entry.Row));
        }

        public sealed override string Name { get; }
        public sealed override string Group => "Git";
    }

    public sealed class Path : Text {
        protected sealed override string Value(GitStatusRow row) => row.Path;
        public Path() : base("Path", 250) { }
    }

    public sealed class Staged : Text {
        protected sealed override string Value(GitStatusRow row) => row.Index;
        public Staged() : base("Staged", 110) { }
    }

    public sealed class WorkTree : Text {
        protected sealed override string Value(GitStatusRow row) => row.WorkTree;
        public WorkTree() : base("Working tree", 130) { }
    }

    public sealed class OriginalPath : Text {
        protected sealed override string Value(GitStatusRow row) => row.OriginalPath;
        public OriginalPath() : base("Original path", 200) { }
    }

    public sealed class Conflict : EntryDataExport<GitStatusEntry, bool> {
        protected sealed override Task<bool> GetValue(GitStatusEntry entry, Action<bool> progress, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(entry.Row.IsConflict);
        }

        public sealed override string Name => "Conflict";
        public sealed override string Group => "Git";
    }
}
