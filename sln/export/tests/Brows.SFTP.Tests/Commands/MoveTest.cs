using Brows;
using Brows.Composition;
using Brows.Panels;
using Brows.SFTP.Native;
using System;
using System.Reflection;

namespace Brows.Commands;

[TestFixture]
internal sealed class MoveTest {
    private static T CreateProviderExport<T>(string name) where T : class {
        var type = typeof(SftpProvider).GetNestedType(name, BindingFlags.NonPublic);
        return Activator.CreateInstance(type, nonPublic: true) as T
            ?? throw new InvalidOperationException($"The SFTP provider export {name} could not be created.");
    }

    private static void InitializeCommand(Command command) {
        var providerType = typeof(ICommand).Assembly.GetType(
            "Brows.Commands.CommandCliProvider",
            throwOnError: true);
        var provider = Activator.CreateInstance(providerType, nonPublic: true);
        var property = typeof(Command).GetProperty(
            "CommandCliProvider",
            BindingFlags.Instance | BindingFlags.NonPublic);
        property.SetValue(command, provider);
    }

    [Test]
    public void Work_UnsupportedLinkKeepsSelectedSource() {
        var sourceFactory = new SftpProviderFactory();
        var targetFactory = new SftpProviderFactory();
        var sourceUri = new Uri("sftp://source@127.0.0.1:2222/source/");
        var targetUri = new Uri("sftp://target@127.0.0.1:2222/target/");
        using var sourceLease = sourceFactory.Acquire(sourceUri);
        using var targetLease = targetFactory.Acquire(targetUri);
        var sourceProvider = new SftpProvider(sourceFactory, sourceUri, null, sourceLease);
        var targetProvider = new SftpProvider(targetFactory, targetUri, null, targetLease);
        var link = new SftpEntry(
            sourceProvider,
            new SftpMetadata("linked.txt", 4, 0, 0, 0, 0xA1FF, 0, 0),
            FileProtocolEntryKind.FileLink);
        var active = new TestPanel {
            Column = 0,
            Provider = sourceProvider,
            Services = { CreateProviderExport<IProvideIO>("ProvideIO") }
        };
        var passive = new TestPanel {
            Column = 1,
            Provider = targetProvider,
            Services = { CreateProviderExport<ICopyProvidedIO>("CopyProvidedIO") }
        };
        var context = new TestCommandContext {
            Line = new TestCommandLine { Parameter = "auto" },
            Panels = { Panels = { active, passive } },
            Source = new TestCommandSource { Items = { link } }
        };
        var command = new Move();
        InitializeCommand(command);

        var error = Assert.Throws<NotSupportedException>(() => ((ICommand)command).TriggeredWork(context));

        Assert.That(error.Message, Is.EqualTo("Copying FileLink entries is not supported."));
    }
}
