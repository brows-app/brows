using Brows;
using Brows.Commands;
using Brows.Panels;
using Brows.SFTP.Native;
using Brows.Triggers;
using Domore.Conf.Cli;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands;

[TestFixture]
internal sealed class RenameTest {
    private sealed class TestSftpFileOperations : ISftpFileOperations {
        public List<(string Source, string Destination)> Renames { get; } = [];

        public Task DeleteFile(string path, CancellationToken token) {
            return Task.CompletedTask;
        }

        public Task DeleteDirectory(string path, CancellationToken token) {
            return Task.CompletedTask;
        }

        public Task Rename(string source, string destination, CancellationToken token) {
            Renames.Add((source, destination));
            return Task.CompletedTask;
        }
    }

    private static void InitializeCommand(Rename command, string inputTrigger) {
        var framework = typeof(ICommand).Assembly;
        var cliProviderType = framework.GetType("Brows.Commands.CommandCliProvider", throwOnError: true);
        var cliProvider = Activator.CreateInstance(cliProviderType, nonPublic: true);
        var cliProviderProperty = framework.GetType("Brows.Commands.Command", throwOnError: true).GetProperty(
            "CommandCliProvider",
            BindingFlags.Instance | BindingFlags.NonPublic);
        cliProviderProperty.SetValue(command, cliProvider);

        var triggerProperty = typeof(Command).GetProperty(nameof(Command.Trigger));
        triggerProperty.GetSetMethod(nonPublic: true).Invoke(command, [new TestCommandTrigger(inputTrigger)]);
    }

    private static string PaletteArgument(TestCommandContext context, string inputTrigger) {
        return context.ShownPalette.Single().Input[(inputTrigger.Length + 1)..];
    }

    private static string ParseName(Rename command, string argument) {
        var method = typeof(Command<Rename.Parameter>).GetMethod(
            "ParameterFactory",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var parameter = method.Invoke(command, [argument, null]);
        return ((Rename.Parameter)parameter).Name;
    }

    private static TestCommandContext Context(SftpProvider provider,
                                              SftpEntry entry,
                                              string inputTrigger,
                                              IGesture gesture) {
        return new TestCommandContext {
            Panels = { Panels = { new TestPanel { Column = 0, Provider = provider } } },
            Source = new TestCommandSource { Items = { entry } },
            Gesture = gesture,
            Line = new TestCommandLine { Input = inputTrigger, Command = inputTrigger }
        };
    }

    private static SftpEntry Entry(SftpProvider provider, string name, FileProtocolEntryKind kind) {
        var entryIsDirectory = kind is FileProtocolEntryKind.Directory or FileProtocolEntryKind.DirectoryLink;
        var permissions = entryIsDirectory ? 0x41EDu : 0x81A4u;
        return new SftpEntry(
            provider,
            new SftpMetadata(name, 4, 0, 0, 0, permissions, 0, 0),
            kind);
    }

    [Test]
    public void Rename_F2OpensPaletteWithOnlyTheFileStemSelected() {
        const string inputTrigger = "sftp-rename";
        var factory = new SftpProviderFactory();
        var uri = new Uri("sftp://user@127.0.0.1:2222/root/");
        using var lease = factory.Acquire(uri);
        using var provider = new SftpProvider(factory, uri, null, lease);
        var entry = Entry(provider, "report.tar.gz", FileProtocolEntryKind.File);
        var command = new Rename();
        InitializeCommand(command, inputTrigger);
        var context = Context(provider, entry, inputTrigger, new PressGesture(PressKey.F2));

        var worked = ((ICommand)command).TriggeredWork(context);

        Assert.Multiple(() => {
            Assert.That(worked, Is.True);
            Assert.That(context.ShownPalette, Has.Count.EqualTo(1));
            Assert.That(context.ShownPalette.Single().Input, Is.EqualTo($"{inputTrigger} \"report.tar.gz\""));
            Assert.That(context.ShownPalette.Single().SelectedStart, Is.EqualTo(inputTrigger.Length + 2));
            Assert.That(context.ShownPalette.Single().SelectedLength, Is.EqualTo("report.tar".Length));
        });
    }

    [Test]
    public void Rename_NoNameInvocationRoundTripsCliQuotesAndBackslashes() {
        const string inputTrigger = "sftp-rename";
        const string name = "both 'single' and \"double\" slash\\name.txt";
        var factory = new SftpProviderFactory();
        var uri = new Uri("sftp://user@127.0.0.1:2222/root/");
        using var lease = factory.Acquire(uri);
        using var provider = new SftpProvider(factory, uri, null, lease);
        var entry = Entry(provider, name, FileProtocolEntryKind.File);
        var command = new Rename();
        InitializeCommand(command, inputTrigger);
        var context = Context(provider, entry, inputTrigger, gesture: null);

        var worked = ((ICommand)command).TriggeredWork(context);
        var argument = PaletteArgument(context, inputTrigger);
        var cli = new CliProvider(new CliSetup());
        var cliParsed = cli.Configure(new Rename.Parameter(), argument).Name;

        Assert.Multiple(() => {
            Assert.That(worked, Is.True);
            Assert.That(context.ShownPalette, Has.Count.EqualTo(1));
            Assert.That(cliParsed, Is.EqualTo(name));
            Assert.That(ParseName(command, argument), Is.EqualTo(name));
        });
    }

    [TestCase(" leading and trailing ")]
    [TestCase("   ")]
    public void Rename_PrefilledNamePreservesBoundaryAndOnlySpaces(string name) {
        const string inputTrigger = "sftp-rename";
        var factory = new SftpProviderFactory();
        var uri = new Uri("sftp://user@127.0.0.1:2222/root/");
        using var lease = factory.Acquire(uri);
        using var provider = new SftpProvider(factory, uri, null, lease);
        var entry = Entry(provider, name, FileProtocolEntryKind.File);
        var command = new Rename();
        InitializeCommand(command, inputTrigger);
        var context = Context(provider, entry, inputTrigger, gesture: null);

        Assert.That(((ICommand)command).TriggeredWork(context), Is.True);
        Assert.That(ParseName(command, PaletteArgument(context, inputTrigger)), Is.EqualTo(name));
    }

    [Test]
    public void Rename_UnchangedNameSkipsProviderRename() {
        const string inputTrigger = "sftp-rename";
        const string name = " same name ";
        var factory = new SftpProviderFactory();
        var uri = new Uri("sftp://user@127.0.0.1:2222/root/");
        using var lease = factory.Acquire(uri);
        var fileOperations = new TestSftpFileOperations();
        using var provider = new SftpProvider(factory, uri, null, lease, fileOperations);
        var entry = Entry(provider, name, FileProtocolEntryKind.File);
        var command = new Rename();
        InitializeCommand(command, inputTrigger);
        var context = Context(provider, entry, inputTrigger, gesture: null);
        context.Line = new TestCommandLine {
            Input = inputTrigger,
            Command = inputTrigger,
            Parameter = $"\"{name}\""
        };

        Assert.That(((ICommand)command).TriggeredWork(context), Is.True);
        Assert.That(fileOperations.Renames, Is.Empty);
    }

    [TestCase("directory.txt", FileProtocolEntryKind.Directory)]
    [TestCase(".env.local", FileProtocolEntryKind.File)]
    public void Rename_NoNameInvocationSelectsWholeDirectoriesAndDotfiles(string name,
                                                                          FileProtocolEntryKind kind) {
        const string inputTrigger = "sftp-rename";
        var factory = new SftpProviderFactory();
        var uri = new Uri("sftp://user@127.0.0.1:2222/root/");
        using var lease = factory.Acquire(uri);
        using var provider = new SftpProvider(factory, uri, null, lease);
        var entry = Entry(provider, name, kind);
        var command = new Rename();
        InitializeCommand(command, inputTrigger);
        var context = Context(provider, entry, inputTrigger, gesture: null);

        Assert.That(((ICommand)command).TriggeredWork(context), Is.True);
        Assert.That(context.ShownPalette.Single().SelectedLength, Is.EqualTo(name.Length));
    }

    [Test]
    public void Rename_NamedCliParameterStillParses() {
        const string inputTrigger = "sftp-rename";
        var factory = new SftpProviderFactory();
        var uri = new Uri("sftp://user@127.0.0.1:2222/root/");
        using var lease = factory.Acquire(uri);
        using var provider = new SftpProvider(factory, uri, null, lease);
        var command = new Rename();
        InitializeCommand(command, inputTrigger);

        var parameter = (Rename.Parameter)typeof(Command<Rename.Parameter>)
            .GetMethod("ParameterFactory", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(command, ["Name=destination.txt", null]);

        Assert.That(parameter.Name, Is.EqualTo("destination.txt"));
    }

    private sealed class TestCommandTrigger : ICommandTrigger {
        public IInputTriggerCollection Inputs { get; }
        public IGestureTriggerCollection Gestures { get; } = new TestGestureTriggerCollection();

        public TestCommandTrigger(string input) {
            Inputs = new TestInputTriggerCollection(input);
        }

        public bool Triggered(string input, out IInputTrigger trigger) {
            trigger = Inputs.Main;
            return trigger.Triggered(input);
        }

        public bool Triggered(IGesture gesture, out IGestureTrigger trigger) {
            trigger = null;
            return false;
        }
    }

    private sealed class TestInputTriggerCollection : IInputTriggerCollection {
        private readonly IInputTrigger Trigger;

        public IInputTrigger Main => Trigger;
        public int Count => 1;

        public TestInputTriggerCollection(string input) {
            Trigger = new TestInputTrigger(input);
        }

        public IEnumerator<IInputTrigger> GetEnumerator() {
            yield return Trigger;
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }

    private sealed class TestInputTrigger : IInputTrigger {
        private readonly IReadOnlySet<string> OptionSet;

        public string Input { get; }
        public string Defined => null;
        public IReadOnlySet<string> Aliases { get; } = new HashSet<string>();
        public IReadOnlySet<string> Options => OptionSet;

        public TestInputTrigger(string input) {
            Input = input;
            OptionSet = new HashSet<string> { input };
        }

        public bool Triggered(string input) {
            return OptionSet.Contains(input);
        }
    }

    private sealed class TestGestureTriggerCollection : IGestureTriggerCollection {
        public IGestureTrigger this[IGesture gesture] => null;
        public int Count => 0;

        public IEnumerator<IGestureTrigger> GetEnumerator() {
            yield break;
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}
