using Brows.Commands;
using Brows.Exports;
using Brows.Operations;
using Brows.Panels;
using Brows.Providers;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
[NonParallelizable]
public sealed class StatusCommandTest {
    private sealed class Context {
        public Mock<ICommandContext> Command { get; } = new();
        public Mock<IPanel> Panel { get; } = new();
        public string ProvidedID { get; private set; }
        public Func<IOperationProgress, CancellationToken, Task<bool>> Operation { get; private set; }

        public Context(IProvider provider) {
            IPanel panel = Panel.Object;
            Command.Setup(value => value.HasPanel(out panel)).Returns(true);
            Panel.Setup(value => value.HasProvider(out provider)).Returns(provider != null);
            Panel.Setup(value => value.Provide(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, CancellationToken>((id, token) => ProvidedID = id).ReturnsAsync(true);
            Command.Setup(value => value.Operate(It.IsAny<Func<IOperationProgress, CancellationToken, Task<bool>>>()))
                .Callback<Func<IOperationProgress, CancellationToken, Task<bool>>>(operation => Operation = operation)
                .Returns(true);
        }

        public void Parameter(string parameter) {
            Mock<ICommandLine> line = new();
            line.Setup(value => value.HasParameter(out parameter)).Returns(true);
            ICommandLine commandLine = line.Object;
            Command.Setup(value => value.HasLine(out commandLine)).Returns(true);
        }
    }

    private static IProvider FileSystem(string path) {
        Mock<IProvider> provider = new();
        provider.As<IFileSystemProvider>().SetupGet(value => value.Path).Returns(path);
        return provider.Object;
    }

    private static ICommand CreateCommand() {
        Status command = new();
        PropertyInfo cli = typeof(Command).GetProperty("CommandCliProvider", BindingFlags.Instance | BindingFlags.NonPublic);
        cli.SetValue(command, Activator.CreateInstance(cli.PropertyType, nonPublic: true));
        return command;
    }

    [Test]
    public async Task Status_OpensRepositoryRootFromNestedFilesystemDirectory() {
        using GitTestSandbox sandbox = new();
        string root = sandbox.InitRepository("repo");
        string child = Directory.CreateDirectory(Path.Combine(root, "child")).FullName;
        Context context = new(FileSystem(child));
        Assert.That(CreateCommand().TriggeredWork(context.Command.Object), Is.True);
        Assert.That(await context.Operation(null, CancellationToken.None), Is.True);
        Assert.That(context.ProvidedID, Is.EqualTo(GitStatusProviderFactory.IDFor(root + Path.DirectorySeparatorChar)));
    }

    [Test]
    public async Task Status_ParsesQuotedPathAndNewPanelDestination() {
        using GitTestSandbox sandbox = new();
        string root = sandbox.InitRepository("repo with spaces #% ü");
        Context context = new(null);
        context.Parameter($"\"{root}\" where=panel");
        Mock<ICommander> commander = new();
        ICommander instance = commander.Object;
        context.Command.Setup(value => value.HasCommander(out instance)).Returns(true);
        string provided = null;
        commander.Setup(value => value.AddPanel(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((id, token) => provided = id).ReturnsAsync(true);
        Assert.That(CreateCommand().TriggeredWork(context.Command.Object), Is.True);
        Assert.That(await context.Operation(null, CancellationToken.None), Is.True);
        Assert.That(provided, Is.EqualTo(GitStatusProviderFactory.IDFor(root + Path.DirectorySeparatorChar)));
        Assert.That(context.ProvidedID, Is.Null);
    }

    [Test]
    public async Task Status_ResolvesRelativePathFromActiveDirectory() {
        using GitTestSandbox sandbox = new();
        string parent = sandbox.CreateDirectory("parent");
        string root = sandbox.InitRepository(Path.Combine("parent", "repo"));
        Context context = new(FileSystem(parent));
        context.Parameter("repo");
        Assert.That(CreateCommand().TriggeredWork(context.Command.Object), Is.True);
        Assert.That(await context.Operation(null, CancellationToken.None), Is.True);
        Assert.That(GitStatusProviderFactory.TryPath(context.ProvidedID, out string path), Is.True);
        Assert.That(Path.TrimEndingDirectorySeparator(path), Is.EqualTo(root));
    }

    [Test]
    public async Task Status_OpensCurrentGitStatusRepository() {
        using GitTestSandbox sandbox = new();
        string root = sandbox.InitRepository("repo");
        using GitStatusProvider provider = await GitStatusProviderFactory.Open(root, CancellationToken.None);
        Context context = new(provider);
        Assert.That(CreateCommand().TriggeredWork(context.Command.Object), Is.True);
        Assert.That(await context.Operation(null, CancellationToken.None), Is.True);
        Assert.That(GitStatusProviderFactory.TryPath(context.ProvidedID, out string path), Is.True);
        Assert.That(Path.TrimEndingDirectorySeparator(path), Is.EqualTo(root));
    }

    [Test]
    public async Task Status_ReportsNonRepositoryWithoutNavigating() {
        using GitTestSandbox sandbox = new();
        Context context = new(FileSystem(sandbox.CreateDirectory("ordinary")));
        Assert.That(CreateCommand().TriggeredWork(context.Command.Object), Is.True);
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => context.Operation(null, CancellationToken.None));
        Assert.That(context.ProvidedID, Is.Null);
    }

    [Test]
    public void Status_RequiresPathWhenCurrentProviderHasNoFilesystemDirectory() {
        Context context = new(Mock.Of<IProvider>());
        Assert.That(CreateCommand().TriggeredWork(context.Command.Object), Is.False);
        Assert.That(context.Operation, Is.Null);
    }
}
