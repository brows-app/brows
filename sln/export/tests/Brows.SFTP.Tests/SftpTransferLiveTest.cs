using Brows.Commands;
using Brows.Composition;
using Brows.Entries;
using Brows.Operations;
using Brows.Panels;
using Brows.Providers;
using Brows.SFTP.Native;
using Brows.Security;
using Brows.Url;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
[NonParallelizable]
[Category("Integration")]
internal sealed class SftpTransferLiveTest {
    private ProtocolTestServer Server;
    private SftpNative Native;

    private Uri Uri => new UriBuilder("sftp", Server.Info.Host, Server.Info.SftpPort) {
        UserName = Server.Info.Username,
        Path = "/fixtures/"
    }.Uri;

    private async Task Connect(SftpClient client) {
        await client.Connect(CancellationToken.None);
        using var password = new SecureString();
        foreach (var character in Server.Info.Password) {
            password.AppendChar(character);
        }
        Assert.That(await client.AuthenticateByPassword(password, CancellationToken.None), Is.True);
    }

    private static void InitializeProvider(Provider provider, object config) {
        for (var type = provider.GetType(); type is not null; type = type.BaseType) {
            type.GetProperty("Config", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                ?.SetValue(provider, config);
        }
        var import = new Mock<IImportAgent>();
        import.SetupGet(value => value.Ready).Returns(true);
        import.Setup(value => value.List<IEntryDataExport>(It.IsAny<bool>())).Returns([]);
        import.Setup(value => value.List<IExport>(It.IsAny<bool>())).Returns([]);
        typeof(Provider).GetProperty("Import", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(provider, import.Object);
    }

    private static IProvideIO ProvideExport() {
        return (IProvideIO)Activator.CreateInstance(
            typeof(SftpProvider).GetNestedType("ProvideIO", BindingFlags.NonPublic), true);
    }

    private static bool Move(SftpProvider provider, IProvider target, ICopyProvidedIO copy, SftpEntry entry,
        IOperationProgress progress = null) {
        var active = new TestPanel { Column = 0, Provider = provider, Services = { ProvideExport() } };
        var passive = new TestPanel { Column = 1, Provider = target, Services = { copy } };
        var context = new TestCommandContext {
            Line = new TestCommandLine { Parameter = "auto" },
            Panels = { Panels = { active, passive } },
            Source = new TestCommandSource { Items = { entry } },
            Progress = progress
        };
        var command = new Commands.Move();
        var cliType = typeof(ICommand).Assembly.GetType("Brows.Commands.CommandCliProvider", true);
        typeof(Command).GetProperty("CommandCliProvider", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(command, Activator.CreateInstance(cliType, true));
        return ((ICommand)command).TriggeredWork(context);
    }

    private static IOperationProgress Progress() {
        var progress = new Mock<IOperationProgress>();
        progress.Setup(value => value.Child(
                It.IsAny<string>(), It.IsAny<OperationDelegate>(),
                It.IsAny<OperationFailureBehavior>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, OperationDelegate child, OperationFailureBehavior behavior, CancellationToken token) => {
                try {
                    await child(progress.Object, token);
                    token.ThrowIfCancellationRequested();
                    return new OperationResult(OperationResultKind.Succeeded, null);
                }
                catch (Exception error) when (behavior == OperationFailureBehavior.Record && !token.IsCancellationRequested) {
                    var kind = error is OperationCanceledException ? OperationResultKind.Canceled : OperationResultKind.Failed;
                    return new OperationResult(kind, error);
                }
            });
        return progress.Object;
    }

    [OneTimeSetUp]
    public async Task Start() {
        if (Environment.GetEnvironmentVariable("BROWS_SFTP_INTEGRATION") != "1") {
            Assert.Ignore("Set BROWS_SFTP_INTEGRATION=1 to run disposable server integration tests.");
        }
        Server = await ProtocolTestServer.Start(CancellationToken.None);
        Native = new SftpNative();
        Assert.That(await Native.Loaded(CancellationToken.None), Is.True);
    }

    [OneTimeTearDown]
    public void Stop() {
        Server?.Dispose();
    }

    [TestCase("ftp", false)]
    [TestCase("ftpes", false)]
    [TestCase("ftps", false)]
    [TestCase("ftp", true)]
    [TestCase("ftpes", true)]
    [TestCase("ftps", true)]
    public async Task Copy_MissingSizeAndEmptyFilesReachFtpDestinations(string scheme, bool empty) {
        var name = (empty ? "empty-" : "unknown-size-") + Guid.NewGuid().ToString("N");
        var contents = empty ? "" : "contents with no SIZE attribute";
        File.WriteAllText(Path.Combine(Server.Info.RootPath, "fixtures", name), contents);
        var factory = new SftpProviderFactory();
        using var lease = factory.Acquire(Uri, Server.Info.KnownHostsPath);
        await Connect(lease.Client);
        using var source = new SftpProvider(factory, Uri, null, lease);
        var entries = new List<SftpEntry>();
        await foreach (var entry in source.ListEntries(Uri, CancellationToken.None)) {
            entries.Add(entry);
        }
        var selected = entries.Single(entry => entry.Name == name);
        Assert.That(new SftpStreamSource(selected).StreamLength, Is.EqualTo(empty ? 0 : -1));
        var io = new List<IProvidedIO>();
        Assert.That(await ProvideExport().Work(io, new TestCommandSource { Items = { selected } },
            source, null, CancellationToken.None), Is.True);

        Assert.That(await UrlNative.Load(CancellationToken.None), Is.True);
        var port = scheme switch {
            "ftp" => Server.Info.FtpPort,
            "ftpes" => Server.Info.ExplicitFtpsPort,
            _ => Server.Info.ImplicitFtpsPort
        };
        var targetName = "target-" + Guid.NewGuid().ToString("N");
        var targetPath = Directory.CreateDirectory(Path.Combine(Server.Info.RootPath, "fixtures", targetName)).FullName;
        var targetUri = new UriBuilder(scheme, Server.Info.Host, port, "/fixtures/" + targetName + "/") {
            Query = scheme == "ftpes" ? "brows-tls=explicit" : ""
        }.Uri;
        var assembly = Assembly.Load(scheme == "ftp" ? "Brows.FTP" : "Brows.FTPS");
        var providerName = scheme == "ftp" ? "Brows.FtpProvider" : "Brows.FtpsProvider";
        var type = assembly.GetType(providerName, true);
        var ftpFactory = Activator.CreateInstance(assembly.GetType(providerName + "Factory", true), true);
        using var target = (Provider)Activator.CreateInstance(type, [ftpFactory, targetUri, null]);
        var config = Activator.CreateInstance(Assembly.Load("Brows.FTP.Client").GetType("Brows.FtpConfig", true), true);
        InitializeProvider(target, config);
        var client = (UrlClient)type.GetMethod("ClientAt").Invoke(target, [targetUri]);
        client.Resolve = false;
        client.Credential.Username = Server.Info.Username;
        await client.Credential.ProtectPassword(Server.Info.Password, SecurityDataKind.PlainText, CancellationToken.None);
        client.GetType().GetProperty("CertificateAuthorityFile").SetValue(client, Server.Info.CaPath);
        var copy = (ICopyProvidedIO)Activator.CreateInstance(type.GetNestedType("CopyExport", BindingFlags.NonPublic), true);
        Assert.That(await copy.Work(io, target, Progress(), CancellationToken.None), Is.True);
        Assert.That(File.ReadAllText(Path.Combine(targetPath, name)), Is.EqualTo(contents));
    }

    [Test]
    public async Task Move_ToFilesystemWithProgressPreservesUnicodeWhitespaceName() {
        var directoryName = "local-move-" + Guid.NewGuid().ToString("N");
        var sourcePath = Directory.CreateDirectory(Path.Combine(Server.Info.RootPath, "fixtures", directoryName)).FullName;
        File.WriteAllText(Path.Combine(sourcePath, "\u2003"), "SFTP data");
        var uri = new Uri(Uri, directoryName + "/");
        var factory = new SftpProviderFactory();
        using var lease = factory.Acquire(uri, Server.Info.KnownHostsPath);
        await Connect(lease.Client);
        using var source = new SftpProvider(factory, uri, null, lease);
        InitializeProvider(source, new SftpConfig());
        var entries = new List<SftpEntry>();
        await foreach (var entry in source.ListEntries(uri, CancellationToken.None)) {
            entries.Add(entry);
        }
        var targetPath = Path.Combine(Server.Info.RootPath, "local-" + Guid.NewGuid().ToString("N"));
        using var target = new FileSystemTestDestination(targetPath);
        Assert.That(Move(source, target.Provider, target.Copy, entries.Single(), Progress()), Is.True);
        Assert.That(File.ReadAllText(Path.Combine(targetPath, "\u2003")), Is.EqualTo("SFTP data"));
        Assert.That(File.Exists(Path.Combine(sourcePath, "\u2003")), Is.False);
    }

    [TestCase("stat")]
    [TestCase("open")]
    [TestCase("remove")]
    public async Task CanceledRequest_IsTerminalAndOtherPaneAcquiresFreshClient(string operation) {
        var directory = Path.Combine(Server.Info.RootPath, "fixtures");
        File.WriteAllText(Path.Combine(directory, "cancel-a.txt"), "AAA");
        File.WriteAllText(Path.Combine(directory, "cancel-b.txt"), "BBBBBBBBBBB");
        var factory = new SftpProviderFactory();
        using var owner = factory.Acquire(Uri, Server.Info.KnownHostsPath);
        using var otherPane = factory.Acquire(Uri, Server.Info.KnownHostsPath);
        var client = owner.Client;
        await Connect(client);
        Server.HoldSftpRequest(operation, "/fixtures/cancel-a.txt");
        using var cancellation = new CancellationTokenSource();
        var pending = operation switch {
            "stat" => (Task)client.Stat("/fixtures/cancel-a.txt", false, cancellation.Token),
            "open" => client.OpenRead("/fixtures/cancel-a.txt", cancellation.Token),
            "remove" => client.DeleteFile("/fixtures/cancel-a.txt", cancellation.Token),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        try {
            await Server.WaitForSftpRequest(CancellationToken.None);
            using var waiterCancellation = new CancellationTokenSource();
            var waiting = otherPane.Client.Stat("/fixtures/cancel-b.txt", false, waiterCancellation.Token);
            waiterCancellation.Cancel();
            await Assert.CatchAsync<OperationCanceledException>(async () => await waiting);
            Assert.That(client.IsTerminal, Is.False, "Canceling a queued waiter must not abort the owner's request.");
            cancellation.Cancel();
            await Assert.CatchAsync<OperationCanceledException>(async () =>
                await pending.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.That(client.IsTerminal, Is.True);
            await Assert.ThrowsAsync<IOException>(async () =>
                await otherPane.Client.Stat("/fixtures/cancel-b.txt", false, CancellationToken.None));
            using var replacement = factory.Acquire(Uri, Server.Info.KnownHostsPath);
            Assert.That(replacement.Client, Is.Not.SameAs(client));
            await Connect(replacement.Client);
            Assert.That((await replacement.Client.Stat("/fixtures/cancel-b.txt", false,
                CancellationToken.None)).Size, Is.EqualTo(11));
            await using var read = await replacement.Client.OpenRead("/fixtures/cancel-b.txt", CancellationToken.None);
            using var reader = new StreamReader(read);
            Assert.That(await reader.ReadToEndAsync(), Is.EqualTo("BBBBBBBBBBB"));
            Assert.That(File.ReadAllText(Path.Combine(directory, "cancel-b.txt")), Is.EqualTo("BBBBBBBBBBB"));
        }
        finally {
            cancellation.Cancel();
            Server.ReleaseSftpRequest();
        }
    }

    [TestCase("child")]
    [TestCase("file")]
    [TestCase("link")]
    [TestCase("none")]
    public async Task Move_RemovesOnlyAnUnchangedCopiedTree(string change) {
        var name = "move-" + Guid.NewGuid().ToString("N");
        var directory = Directory.CreateDirectory(Path.Combine(Server.Info.RootPath, "fixtures", name)).FullName;
        File.WriteAllText(Path.Combine(directory, "original.txt"), "original");
        if (change == "link" && !Server.Info.SymlinksAvailable) {
            Assert.Ignore("The server environment cannot create symbolic links.");
        }
        var factory = new SftpProviderFactory();
        using var sourceLease = factory.Acquire(Uri, Server.Info.KnownHostsPath);
        await Connect(sourceLease.Client);
        using var provider = new SftpProvider(factory, Uri, null, sourceLease);
        InitializeProvider(provider, new SftpConfig());
        SftpEntry entry = null;
        await foreach (var current in provider.ListEntries(Uri, CancellationToken.None)) {
            if (current.Name == name) {
                entry = current;
            }
        }
        Assert.That(entry, Is.Not.Null);
        var targetUri = new Uri("sftp://other@example.test/");
        using var targetLease = factory.Acquire(targetUri);
        using var target = new SftpProvider(factory, targetUri, null, targetLease);
        var copy = new MutatingCopy(() => {
            switch (change) {
                case "child":
                    File.WriteAllText(Path.Combine(directory, "late.txt"), "not copied");
                    break;
                case "file":
                    File.WriteAllText(Path.Combine(directory, "original.txt"), "modified after copying");
                    break;
                case "link":
                    File.CreateSymbolicLink(Path.Combine(directory, "late-link"), "original.txt");
                    break;
            }
        });
        if (change == "none") {
            Assert.That(Move(provider, target, copy, entry), Is.True);
            Assert.That(Directory.Exists(directory), Is.False);
        }
        else {
            Assert.Catch<Exception>(() => Move(provider, target, copy, entry));
            Assert.That(File.Exists(Path.Combine(directory, "original.txt")), Is.True);
            if (change == "child") {
                Assert.That(File.ReadAllText(Path.Combine(directory, "late.txt")), Is.EqualTo("not copied"));
            }
            if (change == "file") {
                Assert.That(File.ReadAllText(Path.Combine(directory, "original.txt")), Is.EqualTo("modified after copying"));
            }
            if (change == "link") {
                Assert.That(new FileInfo(Path.Combine(directory, "late-link")).LinkTarget, Is.EqualTo("original.txt"));
            }
        }
        Assert.That(copy.Files, Is.EqualTo(new[] { name + "/original.txt" }));
    }

    private sealed class MutatingCopy(Action mutate) : ICopyProvidedIO {
        internal List<string> Files { get; } = [];

        public async Task<bool> Work(IEnumerable<IProvidedIO> io, IProvider target,
            IOperationProgress progress, CancellationToken token) {
            foreach (var provided in io) {
                foreach (var set in provided.StreamSets) {
                    await set.Consume(async (source, stream, _, cancellation) => {
                        if (stream is not null) {
                            await stream.CopyToAsync(Stream.Null, cancellation);
                            Files.Add(source.RelativePath);
                        }
                    }, progress, token);
                }
            }
            mutate();
            return true;
        }
    }
}
