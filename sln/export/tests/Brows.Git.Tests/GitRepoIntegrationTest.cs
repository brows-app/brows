using Brows.Git;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
[NonParallelizable]
internal sealed class GitRepoIntegrationTest {
    private GitTestSandbox Sandbox;

    [SetUp]
    public void CreateSandbox() {
        Sandbox = new GitTestSandbox();
    }

    [TearDown]
    public void DeleteSandbox() {
        Sandbox?.Dispose();
        Sandbox = null;
    }

    [Test]
    public async Task Find_SearchesParentsAndReportsAnUnbornRepository() {
        string repositoryPath = Sandbox.InitRepository("repo");
        string childPath = Directory.CreateDirectory(Path.Combine(repositoryPath, "nested", "child")).FullName;

        GitRepo notSearched = await GitRepo.Find(childPath, search: false, CancellationToken.None);
        Assert.That(notSearched, Is.Null);

        using GitRepo repository = await FindRepository(childPath, search: true);
        GitRepositoryStatus status = await repository.Status(CancellationToken.None);

        Assert.That(NormalizePath(status.WorkDirectory), Is.EqualTo(NormalizePath(repositoryPath)));
        Assert.That(NormalizePath(status.GitDirectory), Is.EqualTo(NormalizePath(Path.Combine(repositoryPath, ".git"))));
        Assert.That(status.Branch, Is.EqualTo("main"));
        Assert.That(status.IsUnborn, Is.True);
        Assert.That(status.IsDetached, Is.False);
        Assert.That(status.IsBare, Is.False);
        Assert.That(status.Upstream, Is.Null.Or.Empty);
        Assert.That(status.Ahead, Is.Null);
        Assert.That(status.Behind, Is.Null);
        Assert.That(status.State, Is.EqualTo(GitRepoState.None));
        Assert.That(status.Files, Is.Empty);
    }

    [Test]
    public async Task Find_ReturnsNullForNonRepositoryPaths() {
        string outside = Sandbox.CreateDirectory("outside");

        Assert.That(await GitRepo.Find(outside, search: false, CancellationToken.None), Is.Null);
        Assert.That(await GitRepo.Find(outside, search: true, CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task Status_ReportsTrackedUntrackedIgnoredAndUnicodeChangesLikeGitCli() {
        string repositoryPath = Sandbox.InitRepository("repo");
        Sandbox.WriteFile(repositoryPath, "tracked.txt", "base\n");
        Sandbox.WriteFile(repositoryPath, "staged-delete.txt", "delete me\n");
        Sandbox.WriteFile(repositoryPath, "worktree-delete.txt", "delete me too\n");
        Sandbox.WriteFile(repositoryPath, ".gitignore", "ignored.txt\n");
        Sandbox.CommitAll(repositoryPath, "Initial files");

        Sandbox.WriteFile(repositoryPath, "tracked.txt", "staged version\n");
        Sandbox.RunGit(repositoryPath, "add", "tracked.txt");
        Sandbox.WriteFile(repositoryPath, "tracked.txt", "worktree version\n");
        Sandbox.RunGit(repositoryPath, "rm", "--quiet", "staged-delete.txt");
        File.Delete(Path.Combine(repositoryPath, "worktree-delete.txt"));
        Sandbox.WriteFile(repositoryPath, "index-new.txt", "staged new file\n");
        Sandbox.RunGit(repositoryPath, "add", "index-new.txt");
        Sandbox.WriteFile(repositoryPath, "untracked/未追跡.txt", "untracked Unicode name\n");
        Sandbox.WriteFile(repositoryPath, "ignored.txt", "ignored file\n");

        using GitRepo repository = await FindRepository(repositoryPath, search: false);
        GitRepositoryStatus status = await repository.Status(CancellationToken.None);
        AssertMatchesGit(repositoryPath, status);

        AssertFileStatus(status, "tracked.txt", GitStatusFlags.IndexModified | GitStatusFlags.WorkTreeModified);
        AssertFileStatus(status, "staged-delete.txt", GitStatusFlags.IndexDeleted);
        AssertFileStatus(status, "worktree-delete.txt", GitStatusFlags.WorkTreeDeleted);
        AssertFileStatus(status, "index-new.txt", GitStatusFlags.IndexNew);
        AssertFileStatus(status, "untracked/未追跡.txt", GitStatusFlags.WorkTreeNew);
        AssertFileStatus(status, "ignored.txt", GitStatusFlags.Ignored);
    }

    [Test]
    public async Task Status_AggregatesStagedDeletionAndUntrackedReplacement() {
        string repositoryPath = Sandbox.InitRepository("repo");
        Sandbox.WriteFile(repositoryPath, "replaced.txt", "tracked file\n");
        Sandbox.CommitAll(repositoryPath, "Initial file");
        Sandbox.RunGit(repositoryPath, "rm", "--cached", "--quiet", "replaced.txt");

        using GitRepo repository = await FindRepository(repositoryPath, search: false);
        GitRepositoryStatus status = await repository.Status(CancellationToken.None);
        GitStatusFlags combined = status.Files
            .Where(file => file.Path == "replaced.txt")
            .Aggregate((GitStatusFlags)0, (flags, file) => flags | file.Status);

        Assert.That((combined & (GitStatusFlags.IndexDeleted | GitStatusFlags.WorkTreeNew)),
            Is.EqualTo(GitStatusFlags.IndexDeleted | GitStatusFlags.WorkTreeNew));
    }

    [Test]
    public async Task Status_ReportsStagedRenameAndOriginalPath() {
        string repositoryPath = Sandbox.InitRepository("repo");
        Sandbox.WriteFile(repositoryPath, "before.txt", "rename me\n");
        Sandbox.CommitAll(repositoryPath, "Initial file");
        Sandbox.RunGit(repositoryPath, "mv", "before.txt", "after.txt");

        using GitRepo repository = await FindRepository(repositoryPath, search: false);
        GitRepositoryStatus status = await repository.Status(CancellationToken.None);

        AssertMatchesGit(repositoryPath, status);
        AssertFileStatus(status, "after.txt", GitStatusFlags.IndexRenamed, "before.txt");
    }

    [Test]
    public async Task Status_ReportsUnstagedRenameAndOriginalPath() {
        string repositoryPath = Sandbox.InitRepository("repo");
        Sandbox.WriteFile(repositoryPath, "before.txt", "rename me\n");
        Sandbox.CommitAll(repositoryPath, "Initial file");
        File.Move(Path.Combine(repositoryPath, "before.txt"), Path.Combine(repositoryPath, "after.txt"));

        using GitRepo repository = await FindRepository(repositoryPath, search: false);
        GitRepositoryStatus status = await repository.Status(CancellationToken.None);

        AssertFileStatus(status, "after.txt", GitStatusFlags.WorkTreeRenamed, "before.txt");
    }

    [Test]
    public async Task Status_CombinesStagedAndUnstagedRenamePaths() {
        string repositoryPath = Sandbox.InitRepository("repo");
        Sandbox.WriteFile(repositoryPath, "A.txt", "rename me\n");
        Sandbox.CommitAll(repositoryPath, "Initial file");
        Sandbox.RunGit(repositoryPath, "mv", "A.txt", "B.txt");
        File.Move(Path.Combine(repositoryPath, "B.txt"), Path.Combine(repositoryPath, "C.txt"));

        using GitRepo repository = await FindRepository(repositoryPath, search: false);
        GitRepositoryStatus status = await repository.Status(CancellationToken.None);

        AssertFileStatus(
            status,
            "C.txt",
            GitStatusFlags.IndexRenamed | GitStatusFlags.WorkTreeRenamed,
            "A.txt");
    }

    [Test]
    public async Task Status_RefreshesAfterWorktreeChanges() {
        string repositoryPath = Sandbox.InitRepository("repo");
        Sandbox.WriteFile(repositoryPath, "tracked.txt", "clean\n");
        Sandbox.CommitAll(repositoryPath, "Initial file");
        using GitRepo repository = await FindRepository(repositoryPath, search: false);

        GitRepositoryStatus before = await repository.Status(CancellationToken.None);
        Assert.That(before.Files, Is.Empty);

        Sandbox.WriteFile(repositoryPath, "tracked.txt", "changed\n");
        Sandbox.WriteFile(repositoryPath, "new.txt", "new\n");
        GitRepositoryStatus after = await repository.Status(CancellationToken.None);

        AssertMatchesGit(repositoryPath, after);
        AssertFileStatus(after, "tracked.txt", GitStatusFlags.WorkTreeModified);
        AssertFileStatus(after, "new.txt", GitStatusFlags.WorkTreeNew);
    }

    [Test]
    public async Task Status_RefreshesAfterIndexResetAndBranchSwitch() {
        string repositoryPath = Sandbox.InitRepository("repo");
        Sandbox.WriteFile(repositoryPath, "tracked.txt", "base\n");
        Sandbox.CommitAll(repositoryPath, "Initial commit");
        using GitRepo repository = await FindRepository(repositoryPath, search: false);

        GitRepositoryStatus initial = await repository.Status(CancellationToken.None);
        Assert.That(initial.Branch, Is.EqualTo("main"));
        Assert.That(initial.Files, Is.Empty);

        Sandbox.WriteFile(repositoryPath, "tracked.txt", "staged change\n");
        Sandbox.RunGit(repositoryPath, "add", "tracked.txt");
        GitRepositoryStatus staged = await repository.Status(CancellationToken.None);
        AssertFileStatus(staged, "tracked.txt", GitStatusFlags.IndexModified);

        Sandbox.RunGit(repositoryPath, "reset", "--hard", "HEAD");
        GitRepositoryStatus reset = await repository.Status(CancellationToken.None);
        Assert.That(reset.Files, Is.Empty);
        Assert.That(reset.Branch, Is.EqualTo("main"));

        Sandbox.RunGit(repositoryPath, "switch", "--create", "other");
        GitRepositoryStatus switched = await repository.Status(CancellationToken.None);
        Assert.That(switched.Branch, Is.EqualTo("other"));
        Assert.That(switched.Files, Is.Empty);
        Sandbox.RunGit(repositoryPath, "branch", "--set-upstream-to=main", "other");
        GitRepositoryStatus tracking = await repository.Status(CancellationToken.None);
        AssertTracking(tracking, "main", ahead: 0, behind: 0);
        Sandbox.RunGit(repositoryPath, "branch", "--unset-upstream", "other");
        GitRepositoryStatus untracked = await repository.Status(CancellationToken.None);
        Assert.That(untracked.Upstream, Is.Null.Or.Empty);
    }

    [Test]
    public async Task Status_LeavesIndexHeadAndConfigBytesAndTimestampsUnchanged() {
        string repositoryPath = Sandbox.InitRepository("repo");
        Sandbox.WriteFile(repositoryPath, "tracked.txt", "clean\n");
        Sandbox.CommitAll(repositoryPath, "Initial file");
        Sandbox.WriteFile(repositoryPath, "tracked.txt", "dirty\n");
        Sandbox.RunGit(repositoryPath, "status", "--porcelain=v1");
        string gitDirectory = Path.Combine(repositoryPath, ".git");
        using GitRepo repository = await FindRepository(repositoryPath, search: false);
        Dictionary<string, FileSnapshot> before = SnapshotFiles(
            Path.Combine(gitDirectory, "index"),
            Path.Combine(gitDirectory, "HEAD"),
            Path.Combine(gitDirectory, "config"));

        GitRepositoryStatus status = await repository.Status(CancellationToken.None);

        AssertFileStatus(status, "tracked.txt", GitStatusFlags.WorkTreeModified);
        AssertSnapshotsUnchanged(before);
        Assert.That(File.Exists(Path.Combine(gitDirectory, "index.lock")), Is.False);
    }

    [Test]
    public async Task Status_ReportsMergeConflictAndMergeOperationState() {
        string repositoryPath = Sandbox.InitRepository("repo");
        Sandbox.WriteFile(repositoryPath, "shared.txt", "base\n");
        Sandbox.CommitAll(repositoryPath, "Base commit");
        Sandbox.RunGit(repositoryPath, "branch", "side");

        Sandbox.WriteFile(repositoryPath, "shared.txt", "main edit\n");
        Sandbox.CommitAll(repositoryPath, "Main edit");
        Sandbox.RunGit(repositoryPath, "switch", "side");
        Sandbox.WriteFile(repositoryPath, "shared.txt", "side edit\n");
        Sandbox.CommitAll(repositoryPath, "Side edit");
        Sandbox.RunGit(repositoryPath, "switch", "main");
        Sandbox.RunGitExpectingFailure(repositoryPath, "merge", "side");

        using GitRepo repository = await FindRepository(repositoryPath, search: false);
        GitRepositoryStatus status = await repository.Status(CancellationToken.None);

        Assert.That(status.State, Is.EqualTo(GitRepoState.Merge));
        AssertFileHasFlag(status, "shared.txt", GitStatusFlags.Conflicted);
        Assert.That(status.Files, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Status_ReportsDetachedHeadAndHeadId() {
        string repositoryPath = Sandbox.InitRepository("repo");
        Sandbox.WriteFile(repositoryPath, "file.txt", "content\n");
        Sandbox.CommitAll(repositoryPath, "Initial commit");
        string head = Sandbox.RunGit(repositoryPath, "rev-parse", "HEAD").StandardOutput.Trim();
        Sandbox.RunGit(repositoryPath, "checkout", "--detach", "HEAD");

        using GitRepo repository = await FindRepository(repositoryPath, search: false);
        GitRepositoryStatus status = await repository.Status(CancellationToken.None);

        Assert.That(status.IsDetached, Is.True);
        Assert.That(status.IsUnborn, Is.False);
        Assert.That(status.HeadId, Is.EqualTo(head));
        Assert.That(status.Branch, Is.Null.Or.Empty);
    }

    [Test]
    public async Task Status_ReportsBareRepositoryMetadata() {
        string barePath = Sandbox.InitRepository("bare.git", bare: true);

        using GitRepo repository = await FindRepository(barePath, search: false);
        GitRepositoryStatus status = await repository.Status(CancellationToken.None);

        Assert.That(status.IsBare, Is.True);
        Assert.That(status.IsUnborn, Is.True);
        Assert.That(NormalizePath(status.GitDirectory), Is.EqualTo(NormalizePath(barePath)));
        Assert.That(status.WorkDirectory, Is.Null.Or.Empty);
    }

    [Test]
    public async Task Find_OpensWorktreeAndReportsItsGitDirectory() {
        string repositoryPath = Sandbox.InitRepository("repo");
        Sandbox.WriteFile(repositoryPath, "file.txt", "content\n");
        Sandbox.CommitAll(repositoryPath, "Initial commit");
        string worktreePath = Sandbox.PathFor("linked-worktree");
        Sandbox.RunGit(repositoryPath, "worktree", "add", "-b", "linked", worktreePath, "HEAD");

        using GitRepo repository = await FindRepository(worktreePath, search: false);
        GitRepositoryStatus status = await repository.Status(CancellationToken.None);
        string gitDirectory = Sandbox.RunGit(worktreePath, "rev-parse", "--absolute-git-dir").StandardOutput.Trim();

        Assert.That(NormalizePath(status.WorkDirectory), Is.EqualTo(NormalizePath(worktreePath)));
        Assert.That(NormalizePath(status.GitDirectory), Is.EqualTo(NormalizePath(gitDirectory)));
        Assert.That(status.Branch, Is.EqualTo("linked"));
        Assert.That(status.IsBare, Is.False);
        Assert.That(status.IsUnborn, Is.False);
        Assert.That(status.Files, Is.Empty);
    }

    [Test]
    public async Task Find_UsesSubmoduleGitDirectoryAndStopsAtSubmoduleRoot() {
        string submodulePath = Sandbox.InitRepository("module-source");
        Sandbox.WriteFile(submodulePath, "module.txt", "module contents\n");
        Sandbox.CommitAll(submodulePath, "Module commit");

        string superprojectPath = Sandbox.InitRepository("superproject");
        Sandbox.WriteFile(superprojectPath, "root.txt", "root contents\n");
        Sandbox.CommitAll(superprojectPath, "Superproject commit");
        Sandbox.RunGit(superprojectPath, "-c", "protocol.file.allow=always", "submodule", "add", submodulePath, "vendor/module");
        string moduleWorktree = Path.Combine(superprojectPath, "vendor", "module");
        Sandbox.RunGit(moduleWorktree, "checkout", "--detach", "HEAD");
        Sandbox.CommitAll(superprojectPath, "Add submodule");

        using GitRepo repository = await FindRepository(moduleWorktree, search: true);
        GitRepositoryStatus status = await repository.Status(CancellationToken.None);
        string gitDirectory = Sandbox.RunGit(moduleWorktree, "rev-parse", "--absolute-git-dir").StandardOutput.Trim();

        Assert.That(NormalizePath(status.WorkDirectory), Is.EqualTo(NormalizePath(moduleWorktree)));
        Assert.That(NormalizePath(status.GitDirectory), Is.EqualTo(NormalizePath(gitDirectory)));
        Assert.That(status.Branch, Is.Null.Or.Empty);
        Assert.That(status.IsDetached, Is.True);
        Assert.That(status.Files, Is.Empty);
        Sandbox.WriteFile(moduleWorktree, "module.txt", "modified module contents\n");
        using GitRepo superproject = await FindRepository(superprojectPath, search: false);
        GitRepositoryStatus dirtySuperproject = await superproject.Status(CancellationToken.None);
        AssertFileHasFlag(dirtySuperproject, "vendor/module", GitStatusFlags.WorkTreeModified);
    }

    [Test]
    public async Task Status_ReportsSameAheadBehindDivergedAndMissingUpstreamStates() {
        string seedPath = Sandbox.InitRepository("seed");
        Sandbox.WriteFile(seedPath, "base.txt", "base\n");
        Sandbox.CommitAll(seedPath, "Base commit");
        string originPath = Sandbox.InitRepository("origin.git", bare: true);
        Sandbox.RunGit(seedPath, "remote", "add", "origin", originPath);
        Sandbox.RunGit(seedPath, "push", "--set-upstream", "origin", "main");

        string localPath = Sandbox.CloneRepository(originPath, "local");
        string peerPath = Sandbox.CloneRepository(originPath, "peer");
        using GitRepo repository = await FindRepository(localPath, search: false);

        GitRepositoryStatus same = await repository.Status(CancellationToken.None);
        AssertTracking(same, upstream: "origin/main", ahead: 0, behind: 0);

        Sandbox.WriteFile(localPath, "local.txt", "local commit\n");
        Sandbox.CommitAll(localPath, "Local commit");
        GitRepositoryStatus ahead = await repository.Status(CancellationToken.None);
        AssertTracking(ahead, upstream: "origin/main", ahead: 1, behind: 0);

        Sandbox.WriteFile(peerPath, "peer.txt", "peer commit\n");
        Sandbox.CommitAll(peerPath, "Peer commit");
        Sandbox.RunGit(peerPath, "push", "origin", "main");
        Sandbox.RunGit(localPath, "fetch", "origin");
        GitRepositoryStatus diverged = await repository.Status(CancellationToken.None);
        AssertTracking(diverged, upstream: "origin/main", ahead: 1, behind: 1);

        Sandbox.RunGit(localPath, "reset", "--hard", "origin/main");
        GitRepositoryStatus sameAgain = await repository.Status(CancellationToken.None);
        AssertTracking(sameAgain, upstream: "origin/main", ahead: 0, behind: 0);

        Sandbox.WriteFile(peerPath, "peer-again.txt", "another peer commit\n");
        Sandbox.CommitAll(peerPath, "Another peer commit");
        Sandbox.RunGit(peerPath, "push", "origin", "main");
        Sandbox.RunGit(localPath, "fetch", "origin");
        GitRepositoryStatus behind = await repository.Status(CancellationToken.None);
        AssertTracking(behind, upstream: "origin/main", ahead: 0, behind: 1);

        Sandbox.RunGit(localPath, "update-ref", "-d", "refs/remotes/origin/main");
        GitRepositoryStatus missing = await repository.Status(CancellationToken.None);
        Assert.That(missing.UpstreamMissing, Is.True);
        Assert.That(missing.Upstream, Is.EqualTo("origin/main"));
        Assert.That(missing.Ahead, Is.Null);
        Assert.That(missing.Behind, Is.Null);
    }

    [Test]
    public async Task Head_AndRemoteComparisonRemainUsableAfterRepositoryDisposal() {
        string seedPath = Sandbox.InitRepository("seed");
        Sandbox.WriteFile(seedPath, "base.txt", "base\n");
        Sandbox.CommitAll(seedPath, "Base commit");
        string originPath = Sandbox.InitRepository("origin.git", bare: true);
        Sandbox.RunGit(seedPath, "remote", "add", "origin", originPath);
        Sandbox.RunGit(seedPath, "push", "--set-upstream", "origin", "main");
        using GitRepo repository = await FindRepository(seedPath, search: false);
        using GitRef head = await repository.Head(CancellationToken.None);

        repository.Dispose();
        Assert.That(head.Name, Is.EqualTo("main"));
        Assert.That(await head.CompareRemote(CancellationToken.None), Is.EqualTo(GitRepoComparison.Same));
    }

    [Test]
    public async Task ConfigEntry_RemainsReadableAfterRepositoryDisposal() {
        string repositoryPath = Sandbox.InitRepository("repo");
        Sandbox.RunGit(repositoryPath, "config", "brows.test", "value");
        using GitRepo repository = await FindRepository(repositoryPath, search: false);
        using GitConfigEntry entry = await repository.ConfigEntry("brows.test", CancellationToken.None);

        repository.Dispose();

        Assert.That(entry.Name, Is.EqualTo("brows.test"));
        Assert.That(entry.Value, Is.EqualTo("value"));
    }

    [Test]
    public async Task CachedNativeProperties_ThrowAfterOwnerDisposal() {
        string repositoryPath = Sandbox.InitRepository("repo");
        Sandbox.RunGit(repositoryPath, "config", "brows.test", "value");
        using GitRepo repository = await FindRepository(repositoryPath, search: false);
        using GitConfigEntry entry = await repository.ConfigEntry("brows.test", CancellationToken.None);

        _ = entry.Name;
        _ = entry.Value;
        _ = entry.SourceKind;
        _ = entry.SourcePath;
        _ = entry.IncludeDepth;
        _ = entry.Level;
        string cachedRepositoryPath = repository.Path;

        entry.Dispose();
        repository.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = entry.Name);
        Assert.Throws<ObjectDisposedException>(() => _ = entry.Value);
        Assert.Throws<ObjectDisposedException>(() => _ = entry.SourceKind);
        Assert.Throws<ObjectDisposedException>(() => _ = entry.SourcePath);
        Assert.Throws<ObjectDisposedException>(() => _ = entry.IncludeDepth);
        Assert.Throws<ObjectDisposedException>(() => _ = entry.Level);
        Assert.That(cachedRepositoryPath, Is.Not.Empty);
        Assert.Throws<ObjectDisposedException>(() => _ = repository.Path);
    }

    [Test]
    public async Task FindAndStatus_ObserveCancellation() {
        string repositoryPath = Sandbox.InitRepository("repo");
        using GitRepo repository = await FindRepository(repositoryPath, search: false);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThatAsync(async () => {
            await GitRepo.Find(repositoryPath, search: false, cancellation.Token);
        }, Throws.InstanceOf<OperationCanceledException>());
        await Assert.ThatAsync(async () => {
            await repository.Status(cancellation.Token);
        }, Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task Status_RejectsUseAfterRepositoryDisposal() {
        string repositoryPath = Sandbox.InitRepository("repo");
        GitRepo repository = await FindRepository(repositoryPath, search: false);
        repository.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await repository.Status(CancellationToken.None));
    }

    private static async Task<GitRepo> FindRepository(string path, bool search) {
        GitRepo repository = await GitRepo.Find(path, search, CancellationToken.None);
        Assert.That(repository, Is.Not.Null, $"Expected a Git repository at '{path}'.");
        return repository;
    }

    private void AssertMatchesGit(string repositoryPath, GitRepositoryStatus actual) {
        Dictionary<string, ExpectedFileStatus> expected = ReadGitPorcelainStatus(repositoryPath);
        Dictionary<string, GitFileStatus> actualByPath = actual.Files.ToDictionary(file => file.Path, StringComparer.Ordinal);

        Assert.That(actualByPath.Keys.OrderBy(path => path, StringComparer.Ordinal),
            Is.EqualTo(expected.Keys.OrderBy(path => path, StringComparer.Ordinal)));
        foreach (KeyValuePair<string, ExpectedFileStatus> entry in expected) {
            Assert.That(actualByPath[entry.Key].Status, Is.EqualTo(entry.Value.Status), entry.Key);
            Assert.That(actualByPath[entry.Key].OriginalPath, Is.EqualTo(entry.Value.OriginalPath), entry.Key);
        }
    }

    private Dictionary<string, ExpectedFileStatus> ReadGitPorcelainStatus(string repositoryPath) {
        GitCommandResult result = Sandbox.RunGit(
            repositoryPath,
            "-c", "core.quotepath=false",
            "status", "--porcelain=v1", "-z", "--untracked-files=all", "--ignored=matching", "--renames");
        string[] records = result.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        Dictionary<string, ExpectedFileStatus> files = new(StringComparer.Ordinal);
        for (int index = 0; index < records.Length; index++) {
            string record = records[index];
            if (record.StartsWith("?? ", StringComparison.Ordinal)) {
                files.Add(record[3..], new ExpectedFileStatus(GitStatusFlags.WorkTreeNew, null));
                continue;
            }
            if (record.StartsWith("!! ", StringComparison.Ordinal)) {
                files.Add(record[3..], new ExpectedFileStatus(GitStatusFlags.Ignored, null));
                continue;
            }

            char indexStatus = record[0];
            char workTreeStatus = record[1];
            string path = record[3..];
            string originalPath = null;
            GitStatusFlags flags = MapPorcelainStatus(indexStatus, isIndex: true) |
                MapPorcelainStatus(workTreeStatus, isIndex: false);
            if (indexStatus == 'R' || workTreeStatus == 'R') {
                originalPath = records[++index];
            }
            files.Add(path, new ExpectedFileStatus(flags, originalPath));
        }
        return files;
    }

    private static GitStatusFlags MapPorcelainStatus(char status, bool isIndex) {
        return status switch {
            ' ' => 0,
            'A' => isIndex ? GitStatusFlags.IndexNew : GitStatusFlags.WorkTreeNew,
            'M' => isIndex ? GitStatusFlags.IndexModified : GitStatusFlags.WorkTreeModified,
            'D' => isIndex ? GitStatusFlags.IndexDeleted : GitStatusFlags.WorkTreeDeleted,
            'R' => isIndex ? GitStatusFlags.IndexRenamed : GitStatusFlags.WorkTreeRenamed,
            'T' => isIndex ? GitStatusFlags.IndexTypeChange : GitStatusFlags.WorkTreeTypeChange,
            'U' => GitStatusFlags.Conflicted,
            _ => throw new InvalidOperationException($"Unexpected git status character '{status}'.")
        };
    }

    private static void AssertFileStatus(
        GitRepositoryStatus status,
        string path,
        GitStatusFlags flags,
        string originalPath = null) {
        GitFileStatus file = status.Files.SingleOrDefault(candidate => candidate.Path == path);
        Assert.That(file, Is.Not.Null, $"Missing status for '{path}'.");
        Assert.That(file.Status, Is.EqualTo(flags), path);
        Assert.That(file.OriginalPath, Is.EqualTo(originalPath), path);
    }

    private static void AssertFileHasFlag(GitRepositoryStatus status, string path, GitStatusFlags flags) {
        GitFileStatus file = status.Files.SingleOrDefault(candidate => candidate.Path == path);
        Assert.That(file, Is.Not.Null, $"Missing status for '{path}'.");
        Assert.That((file.Status & flags) != 0, Is.True, path);
    }

    private static void AssertTracking(
        GitRepositoryStatus status,
        string upstream,
        int ahead,
        int behind) {
        Assert.That(status.Upstream, Is.EqualTo(upstream));
        Assert.That(status.UpstreamMissing, Is.False);
        Assert.That(status.Ahead, Is.EqualTo(ahead));
        Assert.That(status.Behind, Is.EqualTo(behind));
    }

    private static string NormalizePath(string path) {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static Dictionary<string, FileSnapshot> SnapshotFiles(params string[] paths) {
        Dictionary<string, FileSnapshot> snapshots = new(StringComparer.Ordinal);
        foreach (string path in paths) {
            snapshots.Add(path, new FileSnapshot(File.ReadAllBytes(path), File.GetLastWriteTimeUtc(path)));
        }
        return snapshots;
    }

    private static void AssertSnapshotsUnchanged(Dictionary<string, FileSnapshot> before) {
        foreach (KeyValuePair<string, FileSnapshot> entry in before) {
            byte[] currentBytes = File.ReadAllBytes(entry.Key);
            Assert.That(currentBytes.SequenceEqual(entry.Value.Bytes), Is.True, $"Bytes changed: {entry.Key}");
            Assert.That(File.GetLastWriteTimeUtc(entry.Key), Is.EqualTo(entry.Value.LastWriteTimeUtc),
                $"Last-write timestamp changed: {entry.Key}");
        }
    }

    private sealed record ExpectedFileStatus(GitStatusFlags Status, string OriginalPath);
    private sealed record FileSnapshot(byte[] Bytes, DateTime LastWriteTimeUtc);
}
