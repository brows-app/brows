using Brows.Git;
using System.Collections.Generic;

namespace Brows;

internal sealed class GitStatusRow {
    internal const GitStatusFlags IndexFlags =
        GitStatusFlags.IndexNew |
        GitStatusFlags.IndexModified |
        GitStatusFlags.IndexDeleted |
        GitStatusFlags.IndexRenamed |
        GitStatusFlags.IndexTypeChange;

    internal const GitStatusFlags WorkTreeFlags =
        GitStatusFlags.WorkTreeModified |
        GitStatusFlags.WorkTreeDeleted |
        GitStatusFlags.WorkTreeRenamed |
        GitStatusFlags.WorkTreeTypeChange |
        GitStatusFlags.WorkTreeUnreadable;

    internal static string Describe(GitStatusFlags status, bool index) {
        if ((status & GitStatusFlags.Conflicted) != 0) {
            return "Conflict";
        }
        var values = new List<string>();
        if (index) {
            if ((status & GitStatusFlags.IndexNew) != 0) values.Add("Added");
            if ((status & GitStatusFlags.IndexModified) != 0) values.Add("Modified");
            if ((status & GitStatusFlags.IndexDeleted) != 0) values.Add("Deleted");
            if ((status & GitStatusFlags.IndexRenamed) != 0) values.Add("Renamed");
            if ((status & GitStatusFlags.IndexTypeChange) != 0) values.Add("Type changed");
        }
        else {
            if ((status & GitStatusFlags.WorkTreeNew) != 0) values.Add("Untracked");
            if ((status & GitStatusFlags.WorkTreeModified) != 0) values.Add("Modified");
            if ((status & GitStatusFlags.WorkTreeDeleted) != 0) values.Add("Deleted");
            if ((status & GitStatusFlags.WorkTreeRenamed) != 0) values.Add("Renamed");
            if ((status & GitStatusFlags.WorkTreeTypeChange) != 0) values.Add("Type changed");
            if ((status & GitStatusFlags.WorkTreeUnreadable) != 0) values.Add("Unreadable");
            if ((status & GitStatusFlags.Ignored) != 0) values.Add("Ignored");
        }
        return string.Join(", ", values);
    }

    public string Path { get; }
    public string OriginalPath { get; }
    public GitStatusFlags Status { get; }
    public string Index { get; }
    public string WorkTree { get; }
    public bool IsIgnored => (Status & GitStatusFlags.Ignored) != 0;
    public bool IsConflict => (Status & GitStatusFlags.Conflicted) != 0;

    internal GitStatusRow(GitFileStatus file) : this(file.Path, file.OriginalPath, file.Status) {
    }

    internal GitStatusRow(string path, string originalPath, GitStatusFlags status) {
        Path = path;
        OriginalPath = originalPath;
        Status = status;
        Index = Describe(Status, index: true);
        WorkTree = Describe(Status, index: false);
    }
}
