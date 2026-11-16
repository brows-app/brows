using Brows.Panels;
using Brows.Providers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands;

/// <summary>
/// Tests the command that extracts the messages of a mailbox to files.
/// </summary>
[TestFixture]
internal sealed class ExportTest {
    private MBoxTestDirectory _Directory;
    private MBoxProvider _Provider;
    private TestPanel _Panel;
    private TestCommandContext _Context;
    private IReadOnlyList<MBoxEntry> _Entries;
    private MBoxExportPublisher _Publisher;

    [SetUp]
    public async Task SetUp() {
        _Publisher = null;
        _Directory = new MBoxTestDirectory();
        var path = MBoxTestFixture.WriteFile(
            _Directory.Directory,
            "inbox.mbox",
            MBoxTestFixture.Mailbox(
                new[] { "First body.\n", "Second body.\n" },
                new[] {
                    "Subject: Shared subject",
                    "From: sender@example.com",
                }));
        _Provider = await MBoxTestProvider.Create(path, new MBoxConfig());
        await MBoxTestProvider.Refresh(_Provider, CancellationToken.None);
        _Entries = _Provider.Entries.ToList();
        _Panel = new TestPanel {
            Column = 0,
            Provider = _Provider,
        };
        _Context = new TestCommandContext {
            Panels = { Panels = { _Panel } },
            /*
             * The palette shows the observation of the provider as the source of the command,
             * so the command is reachable when the observation is the source.
             */
            Source = new TestCommandSource { Items = { ((IEntryProvider)_Provider).Observation } },
        };
    }

    [TearDown]
    public void TearDown() {
        ((IProvider)_Provider).End();
        _Provider.Dispose();
        _Directory.Dispose();
    }

    private void Select(params MBoxEntry[] entries) {
        foreach (var entry in entries) {
            entry.Select = true;
        }
    }

    private bool Work(string parameter) {
        _Context.Line = new TestCommandLine { Parameter = parameter };
        Export command = new() {
            CommandCliProvider = new CommandCliProvider(),
            Publisher = _Publisher ?? new MBoxExportPublisher(),
        };
        return ((ICommand)command).TriggeredWork(_Context);
    }

    private static string Argument(string path) {
        return $"\"{path}\"";
    }

    private DirectoryInfo TargetDirectory() {
        var directory = _Directory.Directory.CreateSubdirectory("target");
        return directory;
    }

    private FileInfo TargetFile(string contents) {
        var file = new FileInfo(Path.Combine(_Directory.Directory.FullName, "target.eml"));
        File.WriteAllText(file.FullName, contents);
        return file;
    }

    private string[] TargetFiles(DirectoryInfo directory) {
        return directory
            .GetFiles()
            .Select(file => file.Name)
            .Order()
            .ToArray();
    }

    [Test]
    public void Export_TriggeredWork_WritesAMessagePerSelectedMessage() {
        var target = TargetDirectory();
        Select(_Entries[0], _Entries[1]);
        Assert.That(Work(Argument(target.FullName)), Is.True);
        Assert.That(TargetFiles(target), Is.EqualTo(new[] {
            MBoxPath.MessageName(_Entries[0].Record.Offset),
            MBoxPath.MessageName(_Entries[1].Record.Offset),
        }));
    }

    [Test]
    public void Export_TriggeredWork_WritesTheMessageAsAnInternetMessage() {
        var target = TargetDirectory();
        Select(_Entries[0]);
        Work(Argument(target.FullName));
        var text = File.ReadAllText(Path.Combine(
            target.FullName,
            MBoxPath.MessageName(_Entries[0].Record.Offset)));
        using (Assert.EnterMultipleScope()) {
            Assert.That(text, Does.Contain("Subject: Shared subject"));
            Assert.That(text, Does.Contain("First body."));
            Assert.That(text, Does.Not.Contain("Second body."));
        }
    }

    [Test]
    public void Export_TriggeredWork_WritesTheMessageToTheFileWhenOneMessageIsSelected() {
        Select(_Entries[0]);
        var target = Path.Combine(_Directory.Directory.FullName, "message.eml");
        Assert.That(Work(Argument(target)), Is.True);
        var text = File.ReadAllText(target);
        using (Assert.EnterMultipleScope()) {
            Assert.That(text, Does.Contain("First body."));
            Assert.That(text, Does.Not.Contain("Second body."));
        }
    }

    [Test]
    public void Export_TriggeredWork_CreatesNoDirectoryForTheDestinationOfOneMessage() {
        Select(_Entries[0]);
        var target = Path.Combine(_Directory.Directory.FullName, "message.eml");
        Work(Argument(target));
        Assert.That(Directory.Exists(target), Is.False);
    }

    [Test]
    public void Export_TriggeredWork_ReturnsFalseWithoutSelection() {
        var target = TargetDirectory();
        Assert.That(Work(Argument(target.FullName)), Is.False);
    }

    [Test]
    public void Export_TriggeredWork_WritesNothingWithoutSelection() {
        var target = TargetDirectory();
        Work(Argument(target.FullName));
        Assert.That(TargetFiles(target), Is.Empty);
    }

    [Test]
    public void Export_TriggeredWork_ReturnsFalseWhenSeveralMessagesAreSelectedAndTheDestinationIsNotADirectory() {
        Select(_Entries[0], _Entries[1]);
        var destination = Path.Combine(_Directory.Directory.FullName, "message.eml");
        Assert.That(Work(Argument(destination)), Is.False);
    }

    [Test]
    public void Export_TriggeredWork_CreatesNoFileWhenSeveralMessagesAreSelectedAndTheDestinationIsNotADirectory() {
        Select(_Entries[0], _Entries[1]);
        var destination = Path.Combine(_Directory.Directory.FullName, "message.eml");
        Work(Argument(destination));
        Assert.That(File.Exists(destination), Is.False);
    }

    [Test]
    public void Export_TriggeredWork_ReturnsFalseWhenSeveralMessagesAreSelectedAndTheDestinationIsAFile() {
        var target = TargetFile("old contents");
        Select(_Entries[0], _Entries[1]);
        Assert.That(Work(Argument(target.FullName)), Is.False);
    }

    [Test]
    public void Export_TriggeredWork_KeepsTheExistingDestinationWithoutOverwrite() {
        var target = TargetFile("old contents");
        Select(_Entries[0]);
        Assert.That(Work(Argument(target.FullName)), Is.False);
        Assert.That(File.ReadAllText(target.FullName), Is.EqualTo("old contents"));
    }

    [Test]
    public void Export_TriggeredWork_ReplacesTheExistingDestinationWithOverwrite() {
        var target = TargetFile("old contents");
        Select(_Entries[0]);
        Assert.That(Work($"{Argument(target.FullName)} overwrite=true"), Is.True);
        var text = File.ReadAllText(target.FullName);
        using (Assert.EnterMultipleScope()) {
            Assert.That(text, Does.Contain("First body."));
            Assert.That(text, Does.Not.Contain("old contents"));
        }
    }

    [Test]
    public void Export_TriggeredWork_ReturnsFalseWhenTheDestinationIsTheMailbox() {
        Select(_Entries[0]);
        Assert.That(Work(Argument(_Provider.Path)), Is.False);
    }

    [Test]
    public void Export_TriggeredWork_KeepsTheMailboxWhenTheDestinationIsTheMailbox() {
        var before = File.ReadAllText(_Provider.Path);
        Select(_Entries[0]);
        Work(Argument(_Provider.Path));
        Assert.That(File.ReadAllText(_Provider.Path), Is.EqualTo(before));
    }

    [Test]
    public void Export_TriggeredWork_RemovesItsStagingFileWhenTheExportFails() {
        var target = TargetFile("old contents");
        Select(_Entries[0]);
        using (File.Open(target.FullName, FileMode.Open, FileAccess.Read, FileShare.None)) {
            Assert.That(Work($"{Argument(target.FullName)} overwrite=true"), Is.False);
        }
        /*
         * The message is written to a file of its own first, so that a failure never leaves a
         * half-written file at the destination. The file of the operation is removed when the
         * operation fails.
         */
        var files = _Directory.Directory
            .GetFiles()
            .Select(file => file.Name)
            .Order()
            .ToArray();
        Assert.That(files, Is.EqualTo(new[] { "inbox.mbox", "target.eml" }));
    }

    [Test]
    public void Export_TriggeredWork_KeepsTheExistingDestinationWhenTheExportFails() {
        var target = TargetFile("old contents");
        Select(_Entries[0]);
        using (File.Open(target.FullName, FileMode.Open, FileAccess.Read, FileShare.None)) {
            Work($"{Argument(target.FullName)} overwrite=true");
        }
        Assert.That(File.ReadAllText(target.FullName), Is.EqualTo("old contents"));
    }
    [Test]
    public void Export_TriggeredWork_KeepsTheExistingDestinationWhenAtomicReplacementFails() {
        var target = TargetFile("old contents");
        Select(_Entries[0]);
        _Publisher = new MBoxExportPublisher((staging, destination, overwrite) => {
            Assert.That(File.Exists(staging), Is.True);
            Assert.That(overwrite, Is.True);
            throw new IOException("Injected replacement failure.");
        });
        Assert.That(Work($"{Argument(target.FullName)} overwrite=true"), Is.False);
        Assert.That(File.ReadAllText(target.FullName), Is.EqualTo("old contents"));
        Assert.That(_Directory.Directory.GetFiles("*.tmp"), Is.Empty);
    }

    [Test]
    public void Export_TriggeredWork_HandlesDestinationCreationRaceWithoutLeavingStagingFiles() {
        var target = new FileInfo(Path.Combine(_Directory.Directory.FullName, "race.eml"));
        Select(_Entries[0]);
        _Publisher = new MBoxExportPublisher((staging, destination, overwrite) => {
            Assert.That(overwrite, Is.False);
            File.WriteAllText(destination, "raced contents");
        });
        Assert.That(Work(Argument(target.FullName)), Is.False);
        using (Assert.EnterMultipleScope()) {
            Assert.That(File.ReadAllText(target.FullName), Is.EqualTo("raced contents"));
            Assert.That(_Directory.Directory.GetFiles("*.tmp"), Is.Empty);
        }
    }
}
