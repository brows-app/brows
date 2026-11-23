using Brows.Commands;
using Brows.Composition;
using Brows.Conf;
using Brows.Entries;
using Brows.Git;
using Brows.Localization;
using Brows.Panels;
using Brows.Providers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
[NonParallelizable]
public sealed class GitStatusProviderTest {
    private static readonly BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    private static readonly FieldInfo ConfigFileRoot = typeof(ConfigurationPath).GetField("<FileRoot>k__BackingField", PrivateStatic);
    private static readonly FieldInfo ConfigDataRoot = typeof(ConfigurationPath).GetField("<DataRoot>k__BackingField", PrivateStatic);

    private GitTestSandbox ConfigSandbox;
    private string PreviousConfigFileRoot;
    private string PreviousConfigDataRoot;

    private static void AssertRow(
        IReadOnlyDictionary<string, GitStatusEntry> rows,
        string path,
        GitStatusFlags flags,
        string originalPath = null) {
        Assert.That(rows.ContainsKey(path), Is.True, $"Missing row '{path}'.");
        Assert.That(rows[path].Row.Status, Is.EqualTo(flags), path);
        Assert.That(rows[path].Row.OriginalPath, Is.EqualTo(originalPath), path);
    }

    private static GitStatusEntry Find(GitStatusProvider provider, string path) {
        return ObservationObserved(provider).SingleOrDefault(entry => entry.ID == path);
    }

    private static ICommand CreateIncludeIgnoredCommand() {
        IncludeIgnored command = new();
        PropertyInfo cli = typeof(Command).GetProperty(
            "CommandCliProvider",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(cli, Is.Not.Null);
        cli.SetValue(command, Activator.CreateInstance(cli.PropertyType, nonPublic: true));
        return command;
    }

    private static ICommandContext CreateIncludeIgnoredContext(
        Provider provider,
        string parameter = null,
        bool hasLine = false,
        bool hasPanel = true) {
        Mock<ICommandContext> context = new();
        if (!hasPanel) {
            return context.Object;
        }
        Mock<IPanel> panel = new();
        IPanel active = panel.Object;
        context.Setup(value => value.HasPanel(out active)).Returns(true);
        Provider current = provider;
        panel.Setup(value => value.HasProvider(out current)).Returns(provider is not null);
        GitStatusProvider git = provider as GitStatusProvider;
        panel.Setup(value => value.HasProvider<GitStatusProvider>(out git)).Returns(git is not null);
        if (hasLine) {
            Mock<ICommandLine> line = new();
            if (!string.IsNullOrEmpty(parameter)) {
                line.Setup(value => value.HasParameter(out parameter)).Returns(true);
            }
            ICommandLine instance = line.Object;
            context.Setup(value => value.HasLine(out instance)).Returns(true);
        }
        return context.Object;
    }

    private sealed class DifferentGitStatusProvider : Provider<GitStatusEntry, GitStatusConfig> {
        public DifferentGitStatusProvider() : base("other-git-status:") {
        }
    }

    private static async Task WaitUntil(Func<bool> condition) {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition()) {
            if (DateTime.UtcNow >= deadline) {
                Assert.Fail("Timed out while waiting for Git status provider rows to change.");
            }
            await Task.Delay(10);
        }
    }

    private static async Task WaitForObservedCount(GitStatusProvider provider, int count) {
        if (ObservationObserved(provider).Count >= count) {
            return;
        }
        TaskCompletionSource changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler handler = (_, _) => {
            if (ObservationObserved(provider).Count >= count) {
                changed.TrySetResult();
            }
        };
        IEntryObservation observation = ((IEntryProvider)provider).Observation;
        observation.ObservedChanged += handler;
        try {
            if (ObservationObserved(provider).Count >= count) {
                return;
            }
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally {
            observation.ObservedChanged -= handler;
        }
    }

    private static string CreateChangedRepository(GitTestSandbox sandbox) {
        string repository = sandbox.InitRepository("status-repo");
        sandbox.WriteFile(repository, "modified.txt", "original\n");
        sandbox.WriteFile(repository, "staged-delete.txt", "delete from index\n");
        sandbox.WriteFile(repository, "worktree-delete.txt", "delete from worktree\n");
        sandbox.WriteFile(repository, "renamed-from.txt", "rename me\n");
        sandbox.WriteFile(repository, "merge.txt", "base\n");
        sandbox.WriteFile(repository, "replaced.txt", "staged deletion with working replacement\n");
        sandbox.WriteFile(repository, "ignored-staged-delete.txt", "staged deletion of an ignored file\n");
        sandbox.WriteFile(repository, ".gitignore", "ignored/\n");
        sandbox.CommitAll(repository, "Initial files");

        sandbox.WriteFile(repository, ".gitignore", "ignored/\nignored-staged-delete.txt\n");
        sandbox.CommitAll(repository, "Ignore a tracked file");
        sandbox.RunGit(repository, "branch", "side");
        sandbox.WriteFile(repository, "merge.txt", "main edit\n");
        sandbox.CommitAll(repository, "Main edit");
        sandbox.RunGit(repository, "switch", "side");
        sandbox.WriteFile(repository, "merge.txt", "side edit\n");
        sandbox.CommitAll(repository, "Side edit");
        sandbox.RunGit(repository, "switch", "main");
        sandbox.RunGitExpectingFailure(repository, "merge", "side");

        sandbox.WriteFile(repository, "modified.txt", "staged version\n");
        sandbox.RunGit(repository, "add", "modified.txt");
        sandbox.WriteFile(repository, "modified.txt", "working tree version\n");
        sandbox.RunGit(repository, "rm", "--quiet", "staged-delete.txt");
        sandbox.RunGit(repository, "rm", "--cached", "--quiet", "replaced.txt");
        sandbox.RunGit(repository, "rm", "--cached", "--quiet", "ignored-staged-delete.txt");
        File.Delete(Path.Combine(repository, "worktree-delete.txt"));
        sandbox.RunGit(repository, "mv", "renamed-from.txt", "renamed-to.txt");
        sandbox.WriteFile(repository, "untracked.txt", "untracked\n");
        sandbox.WriteFile(repository, "ignored/cache.txt", "ignored\n");
        return repository;
    }

    private async Task<GitStatusProviderLease> CreateProvider(string path) {
        GitStatusProviderFactory factory = CreateFactory();
        IProvider provider = await ((IProviderFactory)factory).CreateFor(
            GitStatusProviderFactory.IDFor(path),
            new Mock<IPanel>().Object,
            CancellationToken.None);
        Assert.That(provider, Is.Not.Null);
        return new GitStatusProviderLease((GitStatusProvider)provider);
    }

    private static async Task<GitStatusProviderLease> Open(string path) {
        GitStatusProvider provider = await GitStatusProviderFactory.Open(path, CancellationToken.None);
        return provider == null ? null : new GitStatusProviderLease(provider);
    }

    private static GitStatusProviderFactory CreateFactory() {
        IEntryDataExport[] data = [
            new GitStatusEntryData.Path(),
            new GitStatusEntryData.Staged(),
            new GitStatusEntryData.WorkTree(),
            new GitStatusEntryData.OriginalPath(),
            new GitStatusEntryData.Conflict()
        ];
        Mock<ITranslationExport> translation = new();
        translation.Setup(value => value.Alias(It.IsAny<string>())).Returns(Array.Empty<string>());
        translation.Setup(value => value.Value(It.IsAny<string>())).Returns((string key) => key);
        Mock<IImportAgent> import = new();
        import.SetupGet(value => value.Ready).Returns(true);
        import.Setup(value => value.List<IEntryDataExport>(It.IsAny<bool>())).Returns(data);
        import
            .Setup(value => value.Find<ITranslationExport>(It.IsAny<bool>(), It.IsAny<bool>()))
            .Returns(translation.Object);

        GitStatusProviderFactory factory = new();
        PropertyInfo property = typeof(ProviderFactory<GitStatusProvider>)
            .GetProperty("Import", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(property, Is.Not.Null);
        property.SetValue(factory, import.Object);
        return factory;
    }

    private static List<GitStatusEntry> ObservationObserved(GitStatusProvider provider) {
        return ((IEntryProvider)provider).Observation.Observed.Cast<GitStatusEntry>().ToList();
    }

    private sealed class GitStatusProviderLease : IAsyncDisposable {
        public GitStatusProvider Provider { get; }

        public GitStatusProviderLease(GitStatusProvider provider) {
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public async ValueTask DisposeAsync() {
            ((IProvider)Provider).End();
            await Provider.Monitoring;
            Provider.Dispose();
        }
    }

    [OneTimeSetUp]
    public void IsolateConfiguration() {
        Assert.That(ConfigFileRoot, Is.Not.Null);
        Assert.That(ConfigDataRoot, Is.Not.Null);
        ConfigSandbox = new GitTestSandbox();
        PreviousConfigFileRoot = ConfigFileRoot.GetValue(null) as string;
        PreviousConfigDataRoot = ConfigDataRoot.GetValue(null) as string;
        ConfigFileRoot.SetValue(null, ConfigSandbox.PathFor("config"));
        ConfigDataRoot.SetValue(null, ConfigSandbox.PathFor("data"));
    }

    [OneTimeTearDown]
    public void RestoreConfiguration() {
        ConfigFileRoot.SetValue(null, PreviousConfigFileRoot);
        ConfigDataRoot.SetValue(null, PreviousConfigDataRoot);
        ConfigSandbox?.Dispose();
        ConfigSandbox = null;
    }

    [Test]
    public async Task Begin_ProjectsRepositoryWideChangesAndDefaultColumns() {
        using GitTestSandbox sandbox = new();
        string repository = CreateChangedRepository(sandbox);
        await using GitStatusProviderLease lease = await CreateProvider(repository);
        GitStatusProvider provider = lease.Provider;
        List<GitStatusEntry> initial = ObservationObserved(provider);
        Assert.That(initial, Is.Empty);

        Task observed = WaitForObservedCount(provider, 8);
        ((IProvider)provider).Begin();
        await observed;

        Dictionary<string, GitStatusEntry> rows = ObservationObserved(provider).ToDictionary(entry => entry.ID, StringComparer.Ordinal);
        Assert.That(rows.Keys, Is.EquivalentTo(new[] {
            "modified.txt", "staged-delete.txt", "worktree-delete.txt", "renamed-to.txt", "untracked.txt", "merge.txt",
            "replaced.txt", "ignored-staged-delete.txt"
        }));
        AssertRow(rows, "modified.txt", GitStatusFlags.IndexModified | GitStatusFlags.WorkTreeModified);
        AssertRow(rows, "staged-delete.txt", GitStatusFlags.IndexDeleted);
        AssertRow(rows, "worktree-delete.txt", GitStatusFlags.WorkTreeDeleted);
        AssertRow(rows, "renamed-to.txt", GitStatusFlags.IndexRenamed, "renamed-from.txt");
        AssertRow(rows, "untracked.txt", GitStatusFlags.WorkTreeNew);
        AssertRow(rows, "replaced.txt", GitStatusFlags.IndexDeleted | GitStatusFlags.WorkTreeNew);
        AssertRow(rows, "ignored-staged-delete.txt", GitStatusFlags.IndexDeleted | GitStatusFlags.Ignored);
        Assert.That(rows["merge.txt"].Row.IsConflict, Is.True);
        Assert.That(rows["modified.txt"].Name, Is.EqualTo("modified.txt"));
        Assert.That(rows["modified.txt"].Row.Path, Is.EqualTo("modified.txt"));

        IEntryDataView view = ((IEntryProvider)provider).Observation.DataView;
        Assert.That(view.Has("Path"), Is.True);
        Assert.That(view.Has("Staged"), Is.True);
        Assert.That(view.Has("WorkTree"), Is.True);
        Assert.That(view.Has("OriginalPath"), Is.True);
        Assert.That(view.Sorting["Conflict"], Is.EqualTo(EntrySortDirection.Descending));
        Assert.That(view.Sorting["Path"], Is.EqualTo(EntrySortDirection.Ascending));
    }

    [Test]
    public async Task Refresh_UpdatesBranchAndRowsThenRemovesEverythingWhenClean() {
        using GitTestSandbox sandbox = new();
        string repository = sandbox.InitRepository("refresh-repo");
        sandbox.WriteFile(repository, "stable.txt", "stable\n");
        sandbox.WriteFile(repository, "changing.txt", "before\n");
        sandbox.WriteFile(repository, "removed.txt", "remove\n");
        sandbox.WriteFile(repository, "obsolete.txt", "keep this\n");
        sandbox.CommitAll(repository, "Initial");

        sandbox.WriteFile(repository, "stable.txt", "stable modified\n");
        sandbox.WriteFile(repository, "changing.txt", "staged\n");
        sandbox.RunGit(repository, "add", "changing.txt");
        sandbox.WriteFile(repository, "obsolete.txt", "temporary change\n");
        await using GitStatusProviderLease lease = await CreateProvider(repository);
        GitStatusProvider provider = lease.Provider;
        Task observed = WaitForObservedCount(provider, 3);
        ((IProvider)provider).Begin();
        await observed;

        GitStatusEntry stable = Find(provider, "stable.txt");
        GitStatusEntry changing = Find(provider, "changing.txt");
        GitStatusEntry obsolete = Find(provider, "obsolete.txt");
        stable.Select = true;
        sandbox.WriteFile(repository, "changing.txt", "unstaged after stage\n");
        File.Delete(Path.Combine(repository, "removed.txt"));
        sandbox.WriteFile(repository, "obsolete.txt", "keep this\n");
        sandbox.WriteFile(repository, "new.txt", "new\n");
        sandbox.RunGit(repository, "switch", "-c", "feature/status");

        await ((IProvider)provider).Refresh(CancellationToken.None);

        Assert.That(provider.Status.Branch, Is.EqualTo("feature/status"));
        Assert.That(Find(provider, "stable.txt"), Is.SameAs(stable));
        Assert.That(stable.Select, Is.True);
        Assert.That(Find(provider, "changing.txt"), Is.SameAs(changing));
        Assert.That(changing.Row.Status, Is.EqualTo(GitStatusFlags.IndexModified | GitStatusFlags.WorkTreeModified));
        await changing[nameof(GitStatusEntryData.Staged)].Ready;
        await changing[nameof(GitStatusEntryData.WorkTree)].Ready;
        Assert.That(changing[nameof(GitStatusEntryData.Staged)].Value, Is.EqualTo("Modified"));
        Assert.That(changing[nameof(GitStatusEntryData.WorkTree)].Value, Is.EqualTo("Modified"));
        Assert.That(Find(provider, "obsolete.txt"), Is.Null);
        Assert.That(Find(provider, "removed.txt"), Is.Not.Null);
        Assert.That(Find(provider, "removed.txt").Row.Status, Is.EqualTo(GitStatusFlags.WorkTreeDeleted));
        Assert.That(ObservationObserved(provider), Does.Not.Contain(obsolete));
        Assert.That(Find(provider, "new.txt"), Is.Not.Null);

        sandbox.CommitAll(repository, "Clean all changes");
        await ((IProvider)provider).Refresh(CancellationToken.None);

        Assert.That(provider.Status.Summary, Is.EqualTo("Working tree clean"));
        Assert.That(ObservationObserved(provider), Is.Empty);
        Assert.That(provider.Error, Is.Null);
        Assert.That(provider.IsRefreshing, Is.False);
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task IncludeIgnored_TogglesWhenArgumentIsOmitted(bool initialValue, bool hasLine) {
        using GitTestSandbox sandbox = new();
        string repository = sandbox.InitRepository("include-ignored-toggle-repo");
        await using GitStatusProviderLease lease = await CreateProvider(repository);
        GitStatusProvider provider = lease.Provider;
        provider.ShowIgnored = initialValue;

        ICommand command = CreateIncludeIgnoredCommand();
        ICommandContext context = CreateIncludeIgnoredContext(provider, parameter: "", hasLine: hasLine);
        Assert.That(command.TriggeredWork(context), Is.True);
        Assert.That(provider.ShowIgnored, Is.EqualTo(!initialValue));
    }

    [TestCase(false, "false", false)]
    [TestCase(false, "true", true)]
    [TestCase(true, "false", false)]
    [TestCase(true, "true", true)]
    public async Task IncludeIgnored_ParsesExplicitValueAndIsIdempotent(
        bool initialValue,
        string parameter,
        bool expectedValue) {
        using GitTestSandbox sandbox = new();
        string repository = sandbox.InitRepository("include-ignored-value-repo");
        await using GitStatusProviderLease lease = await CreateProvider(repository);
        GitStatusProvider provider = lease.Provider;
        provider.ShowIgnored = initialValue;

        ICommand command = CreateIncludeIgnoredCommand();
        ICommandContext context = CreateIncludeIgnoredContext(provider, parameter, hasLine: true);
        Assert.That(command.TriggeredWork(context), Is.True);
        Assert.That(provider.ShowIgnored, Is.EqualTo(expectedValue));
        Assert.That(command.TriggeredWork(context), Is.True);
        Assert.That(provider.ShowIgnored, Is.EqualTo(expectedValue));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task IncludeIgnored_RejectsInvalidValueWithoutChangingState(bool initialValue) {
        using GitTestSandbox sandbox = new();
        string repository = sandbox.InitRepository("include-ignored-invalid-repo");
        await using GitStatusProviderLease lease = await CreateProvider(repository);
        GitStatusProvider provider = lease.Provider;
        provider.ShowIgnored = initialValue;

        ICommand command = CreateIncludeIgnoredCommand();
        ICommandContext context = CreateIncludeIgnoredContext(provider, "sometimes", hasLine: true);
        Assert.That(command.TriggeredWork(context), Is.False);
        Assert.That(provider.ShowIgnored, Is.EqualTo(initialValue));
    }

    [Test]
    public async Task IncludeIgnored_RequiresAnActiveGitStatusProvider() {
        using GitTestSandbox sandbox = new();
        string repository = sandbox.InitRepository("include-ignored-scope-repo");
        await using GitStatusProviderLease lease = await CreateProvider(repository);
        ICommand command = CreateIncludeIgnoredCommand();

        Assert.That(command.TriggeredWork(CreateIncludeIgnoredContext(null, hasPanel: false)), Is.False);
        Assert.That(command.TriggeredWork(CreateIncludeIgnoredContext(null)), Is.False);
        using DifferentGitStatusProvider other = new();
        Assert.That(command.TriggeredWork(CreateIncludeIgnoredContext(other)), Is.False);
        Assert.That(lease.Provider.ShowIgnored, Is.False);
    }

    [Test]
    public async Task ShowIgnored_AddsAndRemovesIgnoredRows() {
        using GitTestSandbox sandbox = new();
        string repository = sandbox.InitRepository("ignored-repo");
        sandbox.WriteFile(repository, ".gitignore", "ignored/\n");
        sandbox.CommitAll(repository, "Ignore generated files");
        sandbox.WriteFile(repository, "ignored/cache.txt", "ignored\n");
        sandbox.WriteFile(repository, "visible.txt", "visible\n");

        await using GitStatusProviderLease lease = await CreateProvider(repository);
        GitStatusProvider provider = lease.Provider;
        Task observed = WaitForObservedCount(provider, 1);
        ((IProvider)provider).Begin();
        await observed;
        Assert.That(Find(provider, "ignored/cache.txt"), Is.Null);

        ICommand command = CreateIncludeIgnoredCommand();
        ICommandContext include = CreateIncludeIgnoredContext(provider, "true", hasLine: true);
        Assert.That(command.TriggeredWork(include), Is.True);
        await WaitUntil(() => ObservationObserved(provider).Any(entry => entry.ID == "ignored/"));
        Assert.That(Find(provider, "ignored/").Row.IsIgnored, Is.True);

        ICommandContext exclude = CreateIncludeIgnoredContext(provider);
        Assert.That(command.TriggeredWork(exclude), Is.True);
        await WaitUntil(() => ObservationObserved(provider).All(entry => entry.ID != "ignored/"));
        Assert.That(ObservationObserved(provider), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Monitoring_RefreshesChangesAndStopsAfterEnd() {
        using GitTestSandbox sandbox = new();
        string repository = sandbox.InitRepository("monitor-repo");
        sandbox.WriteFile(repository, "tracked.txt", "before\n");
        sandbox.CommitAll(repository, "Initial");
        await using GitStatusProviderLease lease = await CreateProvider(repository);
        GitStatusProvider provider = lease.Provider;

        ((IProvider)provider).Begin();
        await WaitUntil(() => provider.Monitoring != Task.CompletedTask);
        Task monitoring = provider.Monitoring;
        sandbox.WriteFile(repository, "tracked.txt", "changed while monitoring\n");
        await WaitUntil(() => Find(provider, "tracked.txt") != null);

        GitStatusEntry tracked = Find(provider, "tracked.txt");
        Assert.That(tracked.Row.Status, Is.EqualTo(GitStatusFlags.WorkTreeModified));
        await tracked[nameof(GitStatusEntryData.WorkTree)].Ready;
        Assert.That(tracked[nameof(GitStatusEntryData.WorkTree)].Value, Is.EqualTo("Modified"));

        ((IProvider)provider).End();
        await monitoring.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(monitoring.IsCompleted, Is.True);
        GitStatusView stoppedStatus = provider.Status;

        sandbox.RunGit(repository, "switch", "-c", "after-end");
        await Task.Delay(TimeSpan.FromMilliseconds(2200));
        Assert.That(provider.Status, Is.SameAs(stoppedStatus));
        Assert.That(provider.Status.Branch, Is.EqualTo("main"));
    }

    [Test]
    public async Task Reload_ReportsErrorsRecoversAndHonorsCancellation() {
        using GitTestSandbox sandbox = new();
        string repository = sandbox.InitRepository("error-repo");
        sandbox.WriteFile(repository, "tracked.txt", "contents\n");
        sandbox.CommitAll(repository, "Initial");
        await using GitStatusProviderLease lease = await CreateProvider(repository);
        GitStatusProvider provider = lease.Provider;

        using CancellationTokenSource canceled = new();
        canceled.Cancel();
        await Assert.ThatAsync(async () => await provider.Reload(canceled.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(provider.IsRefreshing, Is.False);
        Assert.That(provider.Error, Is.Null);

        string config = Path.Combine(repository, ".git", "config");
        string original = File.ReadAllText(config);
        File.WriteAllText(config, "[unterminated\n");
        try {
            GitException error = await Assert.ThrowsAsync<GitException>(async () => await provider.Reload(CancellationToken.None));
            Assert.That(error.Message, Is.Not.Empty);
            Assert.That(provider.Error, Is.EqualTo(error.Message));
            Assert.That(provider.IsRefreshing, Is.False);
        }
        finally {
            File.WriteAllText(config, original);
        }

        await provider.Reload(CancellationToken.None);
        Assert.That(provider.Error, Is.Null);
        Assert.That(provider.IsRefreshing, Is.False);
    }

    [Test]
    public async Task Factory_RoundTripsEscapedPathsAndOpensBareAndUnbornRepositories() {
        using GitTestSandbox sandbox = new();
        string specialRepository = sandbox.InitRepository("repo space-雪-%-#");
        string id = GitStatusProviderFactory.IDFor(specialRepository);
        Assert.That(id, Does.Contain("%20"));
        Assert.That(id, Does.Contain("%25"));
        Assert.That(id, Does.Contain("%23"));
        Assert.That(GitStatusProviderFactory.TryPath(id, out string decoded), Is.True);
        Assert.That(Path.GetFullPath(decoded), Is.EqualTo(Path.GetFullPath(specialRepository)));

        string outside = sandbox.CreateDirectory("outside-repository");
        Assert.That(await Open(outside), Is.Null);

        await using GitStatusProviderLease unbornLease = await Open(specialRepository);
        GitStatusProvider unborn = unbornLease.Provider;
        Assert.That(Path.GetFullPath(unborn.RepositoryPath), Is.EqualTo(Path.TrimEndingDirectorySeparator(Path.GetFullPath(specialRepository))));
        Assert.That(unborn.Status.Branch, Is.EqualTo("main · no commits"));

        string bare = sandbox.InitRepository("bare repository.git", bare: true);
        await using GitStatusProviderLease bareLease = await Open(bare);
        GitStatusProvider bareProvider = bareLease.Provider;
        Assert.That(Path.GetFullPath(bareProvider.RepositoryPath), Is.EqualTo(Path.TrimEndingDirectorySeparator(Path.GetFullPath(bare))));
        Assert.That(bareProvider.Status.IsBare, Is.True);
        Assert.That(bareProvider.Status.Files, Is.Empty);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("git-status:relative")]
    [TestCase("git-status:http://example.invalid/repository")]
    [TestCase("file:///C:/repository")]
    [TestCase("git-status:not a uri")]
    public async Task Factory_ReturnsNullForInvalidProviderIds(string id) {
        GitStatusProviderFactory factory = CreateFactory();
        IProvider provider = await ((IProviderFactory)factory).CreateFor(
            id,
            new Mock<IPanel>().Object,
            CancellationToken.None);
        Assert.That(provider, Is.Null);
    }


}
