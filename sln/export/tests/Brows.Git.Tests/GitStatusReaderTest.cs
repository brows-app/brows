using Brows.Entries;
using Brows.Exports;
using Brows.Git;
using Brows.Providers;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
[NonParallelizable]
public sealed class GitStatusReaderTest {
    [Test]
    public async Task Read_SharesSnapshotAcrossDirectoriesAndRefreshesExplicitly() {
        using GitTestSandbox sandbox = new();
        string root = sandbox.InitRepository("repo");
        sandbox.WriteFile(root, "child/tracked.txt", "original\n");
        sandbox.CommitAll(root, "Initial");
        GitStatusView first = await GitStatusReader.Read(root, CancellationToken.None);
        GitStatusView child = await GitStatusReader.Read(Path.Combine(root, "child"), CancellationToken.None);
        Assert.That(child, Is.SameAs(first));
        Assert.That(first.Summary, Is.EqualTo("Working tree clean"));
        sandbox.WriteFile(root, "child/tracked.txt", "changed\n");
        GitStatusView changed = await GitStatusReader.Read(root, CancellationToken.None, refresh: true);
        Assert.That(changed.Summary, Is.EqualTo("1 unstaged"));
        Assert.That(first.Files, Is.Empty);
        Assert.That(changed.ForPath(Path.Combine(root, "child"), directory: true),
            Is.EqualTo(GitStatusFlags.WorkTreeModified));
    }

    [Test]
    public async Task Read_ShowsDeletedRenamedIgnoredAndUntrackedPathsWithoutCountingIgnoredAsDirty() {
        using GitTestSandbox sandbox = new();
        string root = sandbox.InitRepository("repo");
        sandbox.WriteFile(root, "deleted.txt", "original\n");
        sandbox.WriteFile(root, "before.txt", "renamed content\n");
        sandbox.WriteFile(root, ".gitignore", "ignored/\n");
        sandbox.CommitAll(root, "Initial");
        sandbox.WriteFile(root, "ignored/cache.txt", "ignored\n");
        GitStatusView clean = await GitStatusReader.Read(root, CancellationToken.None, refresh: true);
        Assert.That(clean.Summary, Is.EqualTo("Working tree clean"));
        Assert.That(clean.ForPath(root, directory: true), Is.EqualTo(GitStatusFlags.Current));
        Assert.That(clean.ForPath(Path.Combine(root, "ignored/cache.txt"), directory: false),
            Is.EqualTo(GitStatusFlags.Ignored));
        File.Delete(Path.Combine(root, "deleted.txt"));
        sandbox.RunGit(root, "mv", "before.txt", "after.txt");
        sandbox.WriteFile(root, "new.txt", "new\n");
        GitStatusView changed = await GitStatusReader.Read(root, CancellationToken.None, refresh: true);
        Assert.That(changed.Summary, Is.EqualTo("1 staged · 1 unstaged · 1 untracked"));
        Assert.That(changed.Files.Single(file => file.Path == "deleted.txt").WorkTree, Is.EqualTo("Deleted"));
        Assert.That(changed.Files.Single(file => file.Path == "after.txt").OriginalPath, Is.EqualTo("before.txt"));
        Assert.That(changed.ForPath(Path.Combine(root, "before.txt"), directory: false),
            Is.EqualTo(GitStatusFlags.IndexRenamed));
    }

    [Test]
    public async Task Columns_ShowFileStatusAndInvalidateOnRefresh() {
        using GitTestSandbox sandbox = new();
        string root = sandbox.InitRepository("repo");
        sandbox.WriteFile(root, "tracked.txt", "original\n");
        sandbox.CommitAll(root, "Initial");
        Mock<IFileSystemEntry> entry = new();
        entry.SetupGet(value => value.Path).Returns(Path.Combine(root, "tracked.txt"));
        entry.SetupGet(value => value.Kind).Returns(FileSystemEntryKind.File);
        IEntryDataDefinition status = new GitFileSystemData.GitStatus();
        IEntryDataDefinition branch = new GitFileSystemData.GitRepoHead();
        Assert.That(await status.GetValue(entry.Object, null, CancellationToken.None), Is.EqualTo("Clean"));
        Assert.That(await branch.GetValue(entry.Object, null, CancellationToken.None), Is.EqualTo("main"));
        sandbox.WriteFile(root, "tracked.txt", "staged\n");
        sandbox.RunGit(root, "add", "tracked.txt");
        sandbox.WriteFile(root, "tracked.txt", "unstaged\n");
        await status.RefreshValue(entry.Object, CancellationToken.None);
        Assert.That(await status.GetValue(entry.Object, null, CancellationToken.None),
            Is.EqualTo("Staged: Modified; Modified"));
    }

    [Test]
    public async Task Read_DirectoryAggregationHonorsPathBoundariesAndNonRepositoryIsEmpty() {
        using GitTestSandbox sandbox = new();
        string root = sandbox.InitRepository("repo");
        sandbox.WriteFile(root, "src/file.txt", "original\n");
        sandbox.WriteFile(root, "src-other/file.txt", "original\n");
        sandbox.CommitAll(root, "Initial");
        sandbox.WriteFile(root, "src-other/file.txt", "changed\n");
        GitStatusView view = await GitStatusReader.Read(root, CancellationToken.None, refresh: true);
        Assert.That(view.ForPath(Path.Combine(root, "src"), directory: true), Is.EqualTo(GitStatusFlags.Current));
        Assert.That(view.ForPath(sandbox.PathFor("outside"), directory: true), Is.Null);
        Assert.That(await GitStatusReader.Read(sandbox.CreateDirectory("outside"), CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task RefreshEntries_UpdatesChildRepositoryColumnsOnlyWhenTheirValueChanges() {
        using GitTestSandbox sandbox = new();
        string root = sandbox.InitRepository("child-repo");
        sandbox.WriteFile(root, "file.txt", "original\n");
        sandbox.CommitAll(root, "Initial");
        Mock<IEntryData> data = new();
        data.SetupGet(value => value.Value).Returns("main");
        data.Setup(value => value.Refresh(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        Mock<IFileSystemEntry> entry = new();
        entry.SetupGet(value => value.Path).Returns(root);
        entry.SetupGet(value => value.Kind).Returns(FileSystemEntryKind.Directory);
        entry.SetupGet(value => value["GitRepoHead"]).Returns(data.Object);
        Mock<IEntryDataView> dataView = new();
        dataView.Setup(value => value.Has("GitRepoHead")).Returns(true);
        Mock<IEntryObservation> observation = new();
        observation.SetupGet(value => value.DataView).Returns(dataView.Object);
        observation.SetupGet(value => value.Observed).Returns(new IEntry[] { entry.Object });
        Mock<IEntryProvider> provider = new();
        provider.SetupGet(value => value.Observation).Returns(observation.Object);
        await GitStatusReader.RefreshEntries(provider.Object, CancellationToken.None);
        data.Verify(value => value.Refresh(It.IsAny<CancellationToken>()), Times.Never);
        sandbox.RunGit(root, "switch", "-c", "feature");
        await GitStatusReader.Read(root, CancellationToken.None, refresh: true);
        await GitStatusReader.RefreshEntries(provider.Object, CancellationToken.None);
        data.Verify(value => value.Refresh(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Read_CorruptRepositoryReportsNativeErrorDetails() {
        using GitTestSandbox sandbox = new();
        string root = sandbox.InitRepository("repo");
        File.WriteAllText(Path.Combine(root, ".git", "config"), "[unterminated\n");
        GitException error = await Assert.ThrowsAsync<GitException>(async () =>
            await GitStatusReader.Read(root, CancellationToken.None, refresh: true));
        Assert.That(error.Error, Is.Not.Zero);
        Assert.That(error.Message, Does.Contain(":"));
        Assert.That(error.Message, Does.Contain("config").IgnoreCase);
    }

    [Test]
    public async Task Read_CancellationDoesNotPoisonSubsequentReads() {
        using GitTestSandbox sandbox = new();
        string root = sandbox.InitRepository("repo");
        using CancellationTokenSource canceled = new();
        canceled.Cancel();
        await Assert.ThatAsync(async () => await GitStatusReader.Read(root, canceled.Token),
            Throws.InstanceOf<System.OperationCanceledException>());
        GitStatusView view = await GitStatusReader.Read(root, CancellationToken.None);
        Assert.That(view.Branch, Is.EqualTo("main · no commits"));
        Assert.That(view.Tracking, Is.EqualTo("No tracking branch"));
    }
}
