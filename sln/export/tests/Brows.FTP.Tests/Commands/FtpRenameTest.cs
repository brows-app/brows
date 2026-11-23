using Brows;
using Brows.DirectoryListing;
using Brows.Panels;
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
internal sealed class FtpRenameTest {
    private const string SourceName = "old space 25% \"quoted\"";
    private const string DestinationName = "new space 100% \"quoted\"";

    [TestCase("ftp", "/root/", false)]
    [TestCase("ftp", "/root/", true)]
    [TestCase("ftp", "/root/parent/", false)]
    [TestCase("ftp", "/root/parent/", true)]
    [TestCase("ftps", "/root/", false)]
    [TestCase("ftps", "/root/", true)]
    [TestCase("ftps", "/root/parent/", false)]
    [TestCase("ftps", "/root/parent/", true)]
    public void Rename_UsesEntryParentForFilesAndDirectories(string scheme, string providerPath, bool isDirectory) {
        using var provider = new TestProvider(new Uri($"{scheme}://example.test{providerPath}"));
        var entry = Entry(provider, SourceName, isDirectory);
        var command = new TestRenameCommand();

        var worked = Work(command, provider, DestinationName, entry);
        var expectedSourcePath = providerPath + SourceName;
        var expectedDestinationPath = providerPath + DestinationName;

        Assert.That(worked, Is.True);
        Assert.That(command.Calls, Has.Count.EqualTo(1));
        Assert.That(command.Calls[0].Source, Is.EqualTo(expectedSourcePath));
        Assert.That(command.Calls[0].Destination, Is.EqualTo(expectedDestinationPath));
        Assert.That(command.RefreshCount, Is.EqualTo(1));
    }

    [Test]
    public void Rename_DoesNothingForMultipleSelectedEntries() {
        using var provider = new TestProvider(new Uri("ftp://example.test/root/"));
        var first = Entry(provider, "first.txt", isDirectory: false);
        var second = Entry(provider, "second.txt", isDirectory: false);
        var command = new TestRenameCommand();

        var worked = Work(command, provider, "destination.txt", first, second);

        Assert.That(worked, Is.False);
        Assert.That(command.Calls, Is.Empty);
        Assert.That(command.RefreshCount, Is.Zero);
    }

    [Test]
    public void Rename_UnchangedNameSkipsRenameAndRefresh() {
        using var provider = new TestProvider(new Uri("ftp://example.test/root/"));
        var entry = Entry(provider, SourceName, isDirectory: false);
        var command = new TestRenameCommand();

        var worked = Work(command, provider, SourceName, entry);

        Assert.Multiple(() => {
            Assert.That(worked, Is.True);
            Assert.That(command.Calls, Is.Empty);
            Assert.That(command.RefreshCount, Is.Zero);
        });
    }

    [TestCase("Brows.FTP", "FtpProviderFactory", "FtpProvider", "ftp://example.test/root/", "rename")]
    [TestCase("Brows.FTPS", "FtpsProviderFactory", "FtpsProvider", "ftps://example.test/root/", "rename")]
    public void Rename_F2OpensPaletteWithOnlyTheFileStemSelected(string assemblyName,
                                                                 string factoryName,
                                                                 string providerName,
                                                                 string providerId,
                                                                 string inputTrigger) {
        using var provider = CreateProvider(assemblyName, factoryName, providerName, providerId);
        var entry = Entry(provider, "report.tar.gz", isDirectory: false);
        var command = CreateCommand(assemblyName, inputTrigger);
        var context = Context(provider, entry, inputTrigger, new PressGesture(PressKey.F2));

        var worked = command.TriggeredWork(context);

        Assert.That(worked, Is.True);
        Assert.That(context.ShownPalette, Has.Count.EqualTo(1));
        Assert.That(context.ShownPalette.Single().Input, Is.EqualTo($"{inputTrigger} \"report.tar.gz\""));
        Assert.That(context.ShownPalette.Single().SelectedStart, Is.EqualTo(inputTrigger.Length + 2));
        Assert.That(context.ShownPalette.Single().SelectedLength, Is.EqualTo("report.tar".Length));
    }

    [TestCase("Brows.FTP", "FtpProviderFactory", "FtpProvider", "ftp://example.test/root/", "rename")]
    [TestCase("Brows.FTPS", "FtpsProviderFactory", "FtpsProvider", "ftps://example.test/root/", "rename")]
    public void Rename_NoNameInvocationRoundTripsCliQuotesAndBackslashes(string assemblyName,
                                                                          string factoryName,
                                                                          string providerName,
                                                                          string providerId,
                                                                          string inputTrigger) {
        const string name = "both 'single' and \"double\" slash\\name.txt";
        using var provider = CreateProvider(assemblyName, factoryName, providerName, providerId);
        var entry = Entry(provider, name, isDirectory: false);
        var command = CreateCommand(assemblyName, inputTrigger);
        var context = Context(provider, entry, inputTrigger, gesture: null);

        var worked = command.TriggeredWork(context);
        var argument = PaletteArgument(context, inputTrigger);
        var cli = new CliProvider(new CliSetup());
        var cliParsed = cli.Configure(new FtpRename<TestProvider>.Parameter(), argument).Name;

        Assert.Multiple(() => {
            Assert.That(worked, Is.True);
            Assert.That(context.ShownPalette, Has.Count.EqualTo(1));
            Assert.That(cliParsed, Is.EqualTo(name));
            Assert.That(ParseName(command, argument), Is.EqualTo(name));
        });
    }

    [TestCase("Brows.FTP", "FtpProviderFactory", "FtpProvider", "ftp://example.test/root/", "rename")]
    [TestCase("Brows.FTPS", "FtpsProviderFactory", "FtpsProvider", "ftps://example.test/root/", "rename")]
    public void Rename_NoNameInvocationPreservesBoundaryAndOnlySpaces(string assemblyName,
                                                                       string factoryName,
                                                                       string providerName,
                                                                       string providerId,
                                                                       string inputTrigger) {
        foreach (var name in new[] { " leading and trailing ", "   " }) {
            using var provider = CreateProvider(assemblyName, factoryName, providerName, providerId);
            var entry = Entry(provider, name, isDirectory: false);
            var command = CreateCommand(assemblyName, inputTrigger);
            var context = Context(provider, entry, inputTrigger, gesture: null);

            Assert.That(command.TriggeredWork(context), Is.True);
            var argument = PaletteArgument(context, inputTrigger);
            Assert.That(ParseName(command, argument), Is.EqualTo(name));
        }
    }

    private static FtpProviderBase CreateProvider(string assemblyName,
                                                   string factoryName,
                                                   string providerName,
                                                   string providerId) {
        var assembly = Assembly.Load(assemblyName);
        var factoryType = assembly.GetType($"Brows.{factoryName}", throwOnError: true);
        var factory = Activator.CreateInstance(factoryType, nonPublic: true);
        var providerType = assembly.GetType($"Brows.{providerName}", throwOnError: true);
        return (FtpProviderBase)Activator.CreateInstance(
            providerType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [factory, new Uri(providerId), null],
            culture: null);
    }

    private static ICommand CreateCommand(string assemblyName, string inputTrigger) {
        var assembly = Assembly.Load(assemblyName);
        var commandType = assembly.GetType("Brows.Commands.Rename", throwOnError: true);
        var command = (ICommand)Activator.CreateInstance(commandType, nonPublic: true);
        InitializeCommand(command);
        var triggerProperty = typeof(Command).GetProperty(nameof(Command.Trigger));
        triggerProperty.GetSetMethod(nonPublic: true).Invoke(command, [new TestCommandTrigger(inputTrigger)]);
        return command;
    }

    private static void InitializeCommand(ICommand command) {
        var framework = typeof(ICommand).Assembly;
        var cliProviderType = framework.GetType("Brows.Commands.CommandCliProvider", throwOnError: true);
        var cliProvider = Activator.CreateInstance(cliProviderType, nonPublic: true);
        var property = framework.GetType("Brows.Commands.Command", throwOnError: true).GetProperty(
            "CommandCliProvider",
            BindingFlags.Instance | BindingFlags.NonPublic);
        property.SetValue(command, cliProvider);
    }

    private static TestCommandContext Context(FtpProviderBase provider,
                                              FtpEntry entry,
                                              string inputTrigger,
                                              IGesture gesture) {
        var panels = new TestPanelCollection();
        panels.Panels.Add(new TestPanel { Column = 0, Provider = provider });
        return new TestCommandContext {
            Panels = panels,
            Source = new TestCommandSource { Items = { entry } },
            Gesture = gesture,
            Line = new TestCommandLine { Input = inputTrigger, Command = inputTrigger }
        };
    }

    private static string PaletteArgument(TestCommandContext context, string inputTrigger) {
        return context.ShownPalette.Single().Input[(inputTrigger.Length + 1)..];
    }

    private static string ParseName(ICommand command, string argument) {
        var method = command.GetType().GetMethod("ParameterFactory", BindingFlags.Instance | BindingFlags.NonPublic);
        var parameter = method.Invoke(command, [argument, null]);
        return (string)parameter.GetType().GetProperty("Name").GetValue(parameter);
    }

    private static FtpEntry Entry(FtpProviderBase provider, string name, bool isDirectory) {
        var flags = isDirectory ? DirectoryListItemFlags.Directory : (DirectoryListItemFlags)0;
        var item = new DirectoryListItem(name, null, null, null, null, null, flags);
        return new FtpEntry(provider, item);
    }

    private static bool Work(TestRenameCommand command,
                             TestProvider provider,
                             string destinationName,
                             params FtpEntry[] entries) {
        var source = new TestCommandSource();
        foreach (var entry in entries) {
            source.Items.Add(entry);
        }
        var panels = new TestPanelCollection();
        panels.Panels.Add(new TestPanel { Column = 0, Provider = provider });
        var context = new TestCommandContext {
            Panels = panels,
            Source = source,
            Line = new TestCommandLine { Parameter = $"'{destinationName}'" }
        };
        InitializeCommand(command);
        return ((ICommand)command).TriggeredWork(context);
    }

    private sealed class TestProvider : FtpProviderBase {
        public TestProvider(Uri uri) : base(new TestFactory(), uri, null) {
        }
    }

    private sealed class TestFactory : IFtpProviderFactory {
        public FtpClientCache Client { get; } = new();
    }

    private sealed class TestRenameCommand : FtpRename<TestProvider> {
        protected override Task Rename(TestProvider provider,
                                       FtpEntry entry,
                                       string destinationPath,
                                       CancellationToken token) {
            Calls.Add((entry.RemotePath, destinationPath));
            return Task.CompletedTask;
        }

        protected override Task Refresh(TestProvider provider, CancellationToken token) {
            RefreshCount++;
            return Task.CompletedTask;
        }

        public List<(string Source, string Destination)> Calls { get; } = [];
        public int RefreshCount { get; private set; }
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
            return Options.Contains(input);
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
