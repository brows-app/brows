using Brows.Composition;
using Brows.IO;
using Brows.Operations;
using Brows.Panels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands;

[TestFixture]
internal sealed class CopyTest {
    private FileSysSandbox Sandbox;
    private FileCopyService CopyService;
    private DirectoryInfo SourceDirectory;
    private DirectoryInfo TargetDirectory;
    private FileSystemProvider SourceProvider;
    private FileSystemProvider TargetProvider;
    private TestPanel ActivePanel;
    private TestPanel PassivePanel;
    private TestCommandSource Source;
    private TestCommandContext Context;

    private FileInfo SourceFile(string name, string contents) {
        string path = Path.Combine(SourceDirectory.FullName, name);
        File.WriteAllText(path, contents);
        return new FileInfo(path);
    }

    private void SourceFiles(params (string name, string contents)[] files) {
        foreach ((string name, string contents) in files) {
            Source.Items.Add(new FileSystemEntry(SourceProvider, SourceFile(name, contents)));
        }
    }

    private bool Work() {
        Copy command = new() { CommandCliProvider = new CommandCliProvider() };
        return ((ICommand)command).TriggeredWork(Context);
    }

    private IReadOnlyList<string> TargetFiles() {
        return TargetDirectory
            .GetFiles()
            .Select(file => file.Name)
            .Order()
            .ToList();
    }

    [SetUp]
    public void SetUp() {
        Sandbox = new();
        CopyService = new FileCopyService();
        SourceDirectory = Directory.CreateDirectory(Path.Combine(Sandbox.Root, "source"));
        TargetDirectory = Directory.CreateDirectory(Path.Combine(Sandbox.Root, "target"));
        SourceProvider = new FileSystemProvider(
            new FileSystemProviderFactory(), SourceDirectory,
            caseSensitive: false, initialCapacity: 0);
        TargetProvider = new FileSystemProvider(
            new FileSystemProviderFactory { CopyFilesToDirectory = CopyService }, TargetDirectory,
            caseSensitive: false, initialCapacity: 0);
        ActivePanel = new TestPanel {
            Column = 0,
            Provider = SourceProvider,
            Services = { new FileSystemProvider.ProvideIO() }
        };
        PassivePanel = new TestPanel {
            Column = 1,
            Provider = TargetProvider,
            Services = { new FileSystemProvider.CopyProvidedIO() }
        };
        Source = new TestCommandSource();
        Context = new TestCommandContext {
            Line = new TestCommandLine { Parameter = "auto" },
            Panels = { Panels = { ActivePanel, PassivePanel } },
            Source = Source
        };
    }

    [TearDown]
    public void TearDown() {
        SourceProvider.Dispose();
        TargetProvider.Dispose();
        Sandbox.Dispose();
    }

    [Test]
    public void Copy_TriggeredWork_CopiesFileToPassiveDirectory() {
        SourceFiles(("source.txt", "file contents"));
        Work();
        Assert.That(File.ReadAllText(Path.Combine(TargetDirectory.FullName, "source.txt")),
            Is.EqualTo("file contents"));
    }

    [Test]
    public void Copy_TriggeredWork_CopiesEverySelectedFileToPassiveDirectory() {
        SourceFiles(
            ("first.txt", "first contents"),
            ("second.txt", "second contents"));
        Work();
        using (Assert.EnterMultipleScope()) {
            Assert.That(File.ReadAllText(Path.Combine(TargetDirectory.FullName, "first.txt")),
                Is.EqualTo("first contents"));
            Assert.That(File.ReadAllText(Path.Combine(TargetDirectory.FullName, "second.txt")),
                Is.EqualTo("second contents"));
        }
    }

    [Test]
    public void Copy_TriggeredWork_ReturnsTrue() {
        SourceFiles(("source.txt", "file contents"));
        Assert.That(Work(), Is.True);
    }

    [Test]
    public void Copy_TriggeredWork_KeepsFileInActiveDirectory() {
        SourceFiles(("source.txt", "file contents"));
        Work();
        Assert.That(File.Exists(Path.Combine(SourceDirectory.FullName, "source.txt")), Is.True);
    }

    [Test]
    public void Copy_TriggeredWork_DoesNotCopyFileThatIsNotInSource() {
        SourceFiles(("selected.txt", "selected contents"));
        SourceFile("other.txt", "other contents");
        Work();
        Assert.That(TargetFiles(), Is.EqualTo(new[] { "selected.txt" }));
    }

    [TestCase("auto")]
    [TestCase("a")]
    [TestCase("next")]
    [TestCase("n")]
    public void Copy_TriggeredWork_CopiesToPassiveDirectoryWhere(string where) {
        SourceFiles(("source.txt", "file contents"));
        Context.Line = new TestCommandLine { Parameter = where };
        Work();
        Assert.That(TargetFiles(), Is.EqualTo(new[] { "source.txt" }));
    }

    [TestCase("previous")]
    [TestCase("p")]
    public void Copy_TriggeredWork_ReturnsFalseWhereNoSuchPanel(string where) {
        SourceFiles(("source.txt", "file contents"));
        Context.Line = new TestCommandLine { Parameter = where };
        Assert.That(Work(), Is.False);
    }

    [TestCase("previous")]
    [TestCase("p")]
    public void Copy_TriggeredWork_CopiesNothingWhereNoSuchPanel(string where) {
        SourceFiles(("source.txt", "file contents"));
        Context.Line = new TestCommandLine { Parameter = where };
        Work();
        Assert.That(TargetFiles(), Is.Empty);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("sideways")]
    public void Copy_TriggeredWork_ReturnsFalseWithoutValidParameter(string parameter) {
        SourceFiles(("source.txt", "file contents"));
        Context.Line = new TestCommandLine { Parameter = parameter };
        Assert.That(Work(), Is.False);
    }

    [Test]
    public void Copy_TriggeredWork_ReturnsFalseWithoutLine() {
        SourceFiles(("source.txt", "file contents"));
        Context.Line = null;
        Assert.That(Work(), Is.False);
    }

    [Test]
    public void Copy_TriggeredWork_ReturnsFalseWithoutSource() {
        SourceFiles(("source.txt", "file contents"));
        Context.Source = null;
        Assert.That(Work(), Is.False);
    }

    [Test]
    public void Copy_TriggeredWork_ReturnsFalseWithoutSourceItem() {
        SourceFile("source.txt", "file contents");
        Assert.That(Work(), Is.False);
    }

    [Test]
    public void Copy_TriggeredWork_ReturnsFalseWithOnlyOnePanel() {
        SourceFiles(("source.txt", "file contents"));
        Context.Panels.Panels.Remove(PassivePanel);
        Assert.That(Work(), Is.False);
    }

    [Test]
    public void Copy_TriggeredWork_ReturnsFalseWithoutProvideService() {
        SourceFiles(("source.txt", "file contents"));
        ActivePanel.Services.Clear();
        Assert.That(Work(), Is.False);
    }

    [Test]
    public void Copy_TriggeredWork_ReturnsFalseWithoutCopyService() {
        SourceFiles(("source.txt", "file contents"));
        PassivePanel.Services.Clear();
        Assert.That(Work(), Is.False);
    }

    [Test]
    public void Copy_TriggeredWork_CopiesNothingWithoutCopyService() {
        SourceFiles(("source.txt", "file contents"));
        PassivePanel.Services.Clear();
        Work();
        Assert.That(TargetFiles(), Is.Empty);
    }

    [Test]
    public void Copy_TriggeredWork_ReturnsFalseWithoutCopyFilesToDirectory() {
        SourceFiles(("source.txt", "file contents"));
        TargetProvider = new FileSystemProvider(
            new FileSystemProviderFactory(), TargetDirectory, caseSensitive: false, initialCapacity: 0);
        PassivePanel.Provider = TargetProvider;
        Assert.That(Work(), Is.False);
    }

    [Test]
    public void Copy_TriggeredWork_ReturnsFalseWhenCopyFilesToDirectoryFails() {
        SourceFiles(("source.txt", "file contents"));
        CopyService.Result = false;
        Assert.That(Work(), Is.False);
    }

    [Test]
    public void Copy_TriggeredWork_ThrowsWhenCanceled() {
        SourceFiles(("source.txt", "file contents"));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Context.Token = cancellation.Token;
        Assert.That(Work, Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void Copy_TriggeredWork_CopiesNothingWhenCanceled() {
        SourceFiles(("source.txt", "file contents"));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Context.Token = cancellation.Token;
        Assert.Catch<OperationCanceledException>(() => Work());
        Assert.That(TargetFiles(), Is.Empty);
    }

    private sealed class FileCopyService : ICopyFilesToDirectory {
        public bool Result { get; set; } = true;

        public Task<bool> Work(IEnumerable<string> files, string directory,
                               IOperationProgress progress, CancellationToken token) {
            ArgumentNullException.ThrowIfNull(files);
            if (Result == false) {
                return Task.FromResult(false);
            }
            foreach (string file in files) {
                token.ThrowIfCancellationRequested();
                File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
            }
            return Task.FromResult(true);
        }
    }
}
