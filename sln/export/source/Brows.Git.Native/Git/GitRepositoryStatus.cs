using System.Collections.Generic;

namespace Brows.Git;

internal sealed class GitRepositoryStatus {
    public string WorkDirectory { get; }
    public string GitDirectory { get; }
    public string Branch { get; }
    public string HeadId { get; }
    public string Upstream { get; }
    public GitRepoState State { get; }
    public bool IsUnborn { get; }
    public bool IsDetached { get; }
    public bool IsBare { get; }
    public bool UpstreamMissing { get; }
    public int? Ahead { get; }
    public int? Behind { get; }
    public IReadOnlyList<GitFileStatus> Files { get; }

    internal GitRepositoryStatus(string workDirectory,
                                 string gitDirectory,
                                 string branch,
                                 string headId,
                                 string upstream,
                                 GitRepoState state,
                                 bool isUnborn,
                                 bool isDetached,
                                 bool isBare,
                                 bool upstreamMissing,
                                 int? ahead,
                                 int? behind,
                                 GitFileStatus[] files) {
        WorkDirectory = workDirectory;
        GitDirectory = gitDirectory;
        Branch = branch;
        HeadId = headId;
        Upstream = upstream;
        State = state;
        IsUnborn = isUnborn;
        IsDetached = isDetached;
        IsBare = isBare;
        UpstreamMissing = upstreamMissing;
        Ahead = ahead;
        Behind = behind;
        Files = Array.AsReadOnly(files);
    }
}
