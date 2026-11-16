using Brows.Commands;
using Brows.Composition;
using Brows.DirectoryListing;
using Brows.Entries;
using Brows.Operations;
using Brows.Panels;
using Brows.Providers;
using Brows.Security;
using Brows.Url;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
[NonParallelizable]
public sealed class FtpClientLiveTest {
    private static readonly CancellationToken Token = CancellationToken.None;

    private static async Task<List<DirectoryListItem>> List(FtpClient client, CancellationToken token) {
        var items = new List<DirectoryListItem>();
        await foreach (var item in client.List(token).WithCancellation(token)) {
            items.Add(item);
        }
        return items;
    }

    private static async Task<byte[]> Download(FtpClient client, CancellationToken token) {
        using var source = await client.Read(null, token);
        using var destination = new MemoryStream();
        await source.CopyToAsync(destination, token);
        return destination.ToArray();
    }

    private static async Task<Exception> Failure(Func<Task> action) {
        try {
            await action();
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            return ex;
        }
        return null;
    }

    private static async Task AssertFails(Func<Task> action) {
        var failure = await Failure(action);
        Assert.That(failure, Is.Not.Null, "Expected the FTP/FTPS operation to fail.");
    }

    private static bool TestServerEnabled {
        get {
            var value = Environment.GetEnvironmentVariable("BROWS_PROTOCOL_TEST_SERVERS");
            return value is "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    private ProtocolTestServer Server;

    private async Task<FtpClient> Client(string scheme, int port, string caPath = null, string host = null,
                                         string password = null, string path = null, string username = null,
                                         bool? resolve = false) {
        var uri = new UriBuilder(
            scheme,
            host ?? Server.Info.Host,
            port,
            path ?? Server.Info.FixturePath.TrimEnd('/') + '/') {
            Query = scheme.Equals("ftpes", StringComparison.OrdinalIgnoreCase)
                ? "brows-tls=explicit"
                : ""
        }.Uri;
        var client = new FtpClient {
            Uri = uri,
            CertificateAuthorityFile = caPath
        };
        if (resolve.HasValue) {
            client.Resolve = resolve.Value;
        }
        client.Credential.Username = username ?? Server.Info.Username;
        await client.Credential.ProtectPassword(
            password ?? Server.Info.Password,
            SecurityDataKind.PlainText,
            Token);
        return client;
    }

    private async Task<TestProvider> CopyProvider(
        string scheme,
        int port,
        string path,
        string username = null,
        string uriUsername = null,
        bool useServerUsername = true) {
        var uriBuilder = new UriBuilder(scheme, Server.Info.Host, port, path) {
            Query = scheme.Equals("ftpes", StringComparison.OrdinalIgnoreCase)
                ? "brows-tls=explicit"
                : ""
        };
        if (uriUsername is not null) {
            uriBuilder.UserName = Uri.EscapeDataString(uriUsername);
        }
        var uri = uriBuilder.Uri;
        var provider = new TestProvider(uri);
        SetProviderConfig(provider);
        var client = provider.ClientAt(uri);
        client.Resolve = false;
        client.CertificateAuthorityFile = scheme.Equals("ftp", StringComparison.OrdinalIgnoreCase)
            ? null
            : Server.Info.CaPath;
        if (username is not null || useServerUsername) {
            client.Credential.Username = username ?? Server.Info.Username;
        }
        await client.Credential.ProtectPassword(Server.Info.Password, SecurityDataKind.PlainText, Token);
        return provider;
    }

    private static IProvidedIO CopyInput(params IEntryStreamSource[] sources) {
        return new ProvidedIO {
            StreamSets = [new TestStreamSet(sources)]
        };
    }

    private static bool Move(
        TestProvider sourceProvider,
        IProvider targetProvider,
        IProviderExport copy,
        params FtpEntry[] entries) {
        var active = new TestPanel { Column = 0, Provider = sourceProvider };
        active.Services.Add(sourceProvider.ProvideIOExport);
        var passive = new TestPanel { Column = 1, Provider = targetProvider };
        passive.Services.Add(copy);
        var panels = new TestPanelCollection();
        panels.Panels.Add(active);
        panels.Panels.Add(passive);
        var source = new TestCommandSource();
        foreach (var entry in entries) {
            source.Items.Add(entry);
        }
        return ((ICommand)new LiveMoveCommand()).TriggeredWork(new TestCommandContext {
            Panels = panels,
            Source = source
        });
    }

    private static void SetProviderConfig(TestProvider provider) {
        var config = new FtpConfig();
        var typedConfigProperty = typeof(Provider<FtpEntry, FtpConfig>).BaseType.GetProperty(
            "Config",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        var baseConfigProperty = typeof(EntryProvider).GetProperty(
            "Config",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        if (typedConfigProperty is null || baseConfigProperty is null) {
            throw new InvalidOperationException("The test provider configuration properties were not found.");
        }
        typedConfigProperty.SetValue(provider, config);
        baseConfigProperty.SetValue(provider, config);
    }

    [OneTimeSetUp]
    public async Task StartServer() {
        if (!TestServerEnabled) {
            Assert.Ignore("Set BROWS_PROTOCOL_TEST_SERVERS=1 to run the live FTP/FTPS integration tests.");
        }
        Server = await ProtocolTestServer.Start(Token);
        var loaded = await UrlNative.Load(Token);
        if (!loaded) {
            Assert.Ignore("The brows_url native DLL is not available beside the test binaries.");
        }
    }

    [OneTimeTearDown]
    public void StopServer() {
        Server?.Dispose();
    }

    [Test]
    public async Task PlainFtp_ListDownloadUploadCommandsAndFailedRenameRetainsSource() {
        var client = await Client("ftp", Server.Info.FtpPort);
        var items = await List(client, Token);
        Assert.That(items.Select(item => item.Name), Does.Contain("alpha.txt"));
        Assert.That(items.Select(item => item.Name), Does.Contain("space name.txt"));
        Assert.That(items.Select(item => item.Name), Does.Contain("nested"));
        Assert.That(items.Select(item => item.Name), Does.Contain("empty"));

        var fileClient = await Client("ftp", Server.Info.FtpPort, path: Server.Info.FixturePath + "/alpha.txt");
        var downloaded = await Download(fileClient, Token);
        Assert.That(Encoding.UTF8.GetString(downloaded), Is.EqualTo("alpha fixture\n"));
        var spacedFileClient = await Client(
            "ftp",
            Server.Info.FtpPort,
            path: Server.Info.FixturePath + "/space name.txt");
        Assert.That(
            Encoding.UTF8.GetString(await Download(spacedFileClient, Token)),
            Is.EqualTo("file name contains spaces\n"));

        var uploadPath = Server.Info.FixturePath + "/uploaded space.txt";
        var upload = Encoding.UTF8.GetBytes("uploaded through libcurl\n");
        await using (var stream = await client.Write(uploadPath, upload.Length, Token)) {
            await stream.WriteAsync(upload, Token);
        }
        await client.CreateDirectory(Server.Info.FixturePath + "/created folder", Token);
        await client.Rename(uploadPath, Server.Info.FixturePath + "/renamed space.txt", Token);

        items = await List(client, Token);
        Assert.That(items.Select(item => item.Name), Does.Contain("renamed space.txt"));
        Assert.That(items.Select(item => item.Name), Does.Contain("created folder"));
        await client.Delete(Server.Info.FixturePath + "/renamed space.txt", directory: false, Token);
        await client.Delete(Server.Info.FixturePath + "/created folder", directory: true, Token);

        await AssertFails(async () => await client.Rename(
            Server.Info.FixturePath + "/alpha.txt",
            Server.Info.FixturePath + "/missing/alpha.txt",
            Token));
        items = await List(client, Token);
        Assert.That(items.Select(item => item.Name), Does.Contain("alpha.txt"));
    }

    [TestCase("ftp")]
    [TestCase("ftpes")]
    [TestCase("ftps")]
    public async Task DeleteRecursive_RemovesDirectoriesAndUnlinksLinks(string scheme) {
        var symlinksAvailable = Server.Info.SymlinksAvailable;
        var port = scheme switch {
            "ftp" => Server.Info.FtpPort,
            "ftpes" => Server.Info.ExplicitFtpsPort,
            "ftps" => Server.Info.ImplicitFtpsPort,
            _ => throw new ArgumentOutOfRangeException(nameof(scheme))
        };
        var caPath = scheme == "ftp" ? null : Server.Info.CaPath;
        var fixturePath = Path.Combine(Server.Info.RootPath, "fixtures");
        var name = "delete-tree-" + Guid.NewGuid().ToString("N");
        var treePath = Path.Combine(fixturePath, name);
        var targetDirectoryPath = Path.Combine(fixturePath, name + "-directory-target");
        var targetFilePath = Path.Combine(fixturePath, name + "-file-target.txt");
        Directory.CreateDirectory(Path.Combine(treePath, "nested"));
        Directory.CreateDirectory(Path.Combine(treePath, "empty"));
        Directory.CreateDirectory(targetDirectoryPath);
        File.WriteAllText(Path.Combine(treePath, "root.txt"), "tree root\n");
        File.WriteAllText(Path.Combine(treePath, "nested", "child.txt"), "nested child\n");
        File.WriteAllText(Path.Combine(targetDirectoryPath, "target.txt"), "directory target\n");
        File.WriteAllText(targetFilePath, "file target\n");
        if (symlinksAvailable) {
            Directory.CreateSymbolicLink(Path.Combine(treePath, "directory link"), targetDirectoryPath);
            File.CreateSymbolicLink(Path.Combine(treePath, "file link.txt"), targetFilePath);
        }

        var client = await Client(scheme, port, caPath, path: Server.Info.FixturePath + "/");
        using var provider = new LiveDeleteProvider(client.Uri, client);
        var tree = new FtpEntry(
            provider,
            new DirectoryListItem(name, null, null, null, null, null, DirectoryListItemFlags.Directory),
            FileProtocolUri.Extend(provider.Uri, [name], includeTrailingSlash: true));
        var children = new List<FtpEntry>();
        await foreach (var child in provider.ListEntries(tree.Uri, Token).WithCancellation(Token)) {
            children.Add(child);
        }

        if (symlinksAvailable) {
            Assert.Multiple(() => {
                Assert.That(children.Single(item => item.Name == "directory link").Kind,
                    Is.EqualTo(FileProtocolEntryKind.DirectoryLink));
                Assert.That(children.Single(item => item.Name == "file link.txt").Kind,
                    Is.EqualTo(FileProtocolEntryKind.FileLink));
            });
        }

        var deleted = new List<string>();
        var deletedCount = await provider.DeleteRecursive(tree, entry => deleted.Add(entry.Name), Token);

        Assert.Multiple(() => {
            Assert.That(deletedCount, Is.EqualTo(symlinksAvailable ? 7 : 5));
            if (symlinksAvailable) {
                Assert.That(deleted, Does.Contain("directory link"));
                Assert.That(deleted, Does.Contain("file link.txt"));
            }
            Assert.That(Directory.Exists(treePath), Is.False);
            Assert.That(
                File.ReadAllText(Path.Combine(targetDirectoryPath, "target.txt")),
                Is.EqualTo("directory target\n"));
            Assert.That(File.ReadAllText(targetFilePath), Is.EqualTo("file target\n"));
        });
    }

    [TestCase("ftp", false)]
    [TestCase("ftp", true)]
    [TestCase("ftpes", false)]
    [TestCase("ftpes", true)]
    public async Task Copy_MergesExistingNestedTreesAndRejectsDirectoryCreationFailures(
        string scheme,
        bool serverAbsolute) {
        var port = scheme == "ftp" ? Server.Info.FtpPort : Server.Info.ExplicitFtpsPort;
        var targetName = "copy-merge-" + Guid.NewGuid().ToString("N");
        var fixtureBasePath = serverAbsolute ? "//fixtures" : Server.Info.FixturePath;
        var targetPath = fixtureBasePath + "/" + targetName + "/";
        var fixtureDirectory = serverAbsolute
            ? Path.Combine(Server.Info.RootPath, "fixtures")
            : Path.Combine(Server.Info.RootPath, "home", "fixtures");
        var targetDirectory = Path.Combine(fixtureDirectory, targetName);
        var existingDirectory = Path.Combine(targetDirectory, "existing", "deep");
        Directory.CreateDirectory(existingDirectory);
        var replacePath = Path.Combine(existingDirectory, "replace.txt");
        File.WriteAllText(replacePath, "original contents\n");

        using var provider = await CopyProvider(scheme, port, targetPath, Server.Info.LoginHomeUsername);
        var input = CopyInput(
            new TestStreamSource("existing/deep/replace.txt", "copied contents\n"),
            new TestStreamSource(" leading folder/ child file.txt", "literal path contents\n"),
            new TestStreamSource("existing/deep/empty", contents: null),
            new TestStreamSource("new empty/inner", contents: null));

        Assert.That(await provider.Copy([input], Token), Is.True);
        Assert.That(File.ReadAllText(replacePath), Is.EqualTo("copied contents\n"));
        Assert.That(File.ReadAllText(Path.Combine(targetDirectory, " leading folder", " child file.txt")),
                    Is.EqualTo("literal path contents\n"));
        Assert.That(Directory.Exists(Path.Combine(existingDirectory, "empty")), Is.True);
        Assert.That(Directory.Exists(Path.Combine(targetDirectory, "new empty", "inner")), Is.True);

        File.WriteAllText(replacePath, "contents changed between copies\n");
        Assert.That(await provider.Copy([input], Token), Is.True);
        Assert.That(File.ReadAllText(replacePath), Is.EqualTo("copied contents\n"));
        Assert.That(Directory.Exists(Path.Combine(existingDirectory, "empty")), Is.True);
        Assert.That(Directory.Exists(Path.Combine(targetDirectory, "new empty", "inner")), Is.True);

        var collisionName = "file-parent-" + Guid.NewGuid().ToString("N");
        var collisionPath = Path.Combine(targetDirectory, collisionName);
        File.WriteAllText(collisionPath, "preserve this file\n");
        var collisionInput = CopyInput(new TestStreamSource(collisionName + "/child.txt", "must fail\n"));
        var collisionFailure = await Failure(async () => {
            _ = await provider.Copy([collisionInput], Token);
        });
        Assert.That(collisionFailure, Is.Not.Null, "A file must not be accepted as an existing directory.");
        Assert.That(File.ReadAllText(collisionPath), Is.EqualTo("preserve this file\n"));

        var readOnlyName = "copy-readonly-" + Guid.NewGuid().ToString("N");
        var readOnlyDirectory = Path.Combine(Server.Info.RootPath, "fixtures", readOnlyName);
        Directory.CreateDirectory(readOnlyDirectory);
        using var readOnlyProvider = await CopyProvider(
            scheme,
            port,
            Server.Info.FixturePath + "/" + readOnlyName + "/",
            Server.Info.Username + "-readonly");
        var deniedInput = CopyInput(new TestStreamSource("denied/file.txt", "must fail\n"));
        var deniedFailure = await Failure(async () => {
            _ = await readOnlyProvider.Copy([deniedInput], Token);
        });
        Assert.That(deniedFailure, Is.Not.Null, "A denied MKD must not be treated as an existing directory.");
        Assert.That(Directory.Exists(Path.Combine(readOnlyDirectory, "denied")), Is.False);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var canceledInput = CopyInput(new TestStreamSource("canceled/file.txt", "must not upload\n"));
        await Assert.ThrowsAsync<OperationCanceledException>(async () => {
            _ = await provider.Copy([canceledInput], cancellation.Token);
        });
        Assert.That(Directory.Exists(Path.Combine(targetDirectory, "canceled")), Is.False);
    }

    [TestCase("ftp")]
    [TestCase("ftpes")]
    [TestCase("ftps")]
    public async Task Copy_RejectsSameFileWhenConfiguredAndUriUsernamesResolveToOneAccount(string scheme) {
        var port = scheme switch {
            "ftp" => Server.Info.FtpPort,
            "ftpes" => Server.Info.ExplicitFtpsPort,
            "ftps" => Server.Info.ImplicitFtpsPort,
            _ => throw new ArgumentOutOfRangeException(nameof(scheme), scheme, "Unsupported FTP test scheme.")
        };
        var name = "self-copy-identity-" + Guid.NewGuid().ToString("N") + ".txt";
        var path = Server.Info.FixturePath + "/";
        var localFile = Path.Combine(Server.Info.RootPath, "fixtures", name);
        const string original = "self-copy source contents must survive\n";
        File.WriteAllText(localFile, original);

        using var sourceProvider = await CopyProvider(scheme, port, path);
        using var targetProvider = await CopyProvider(
            scheme,
            port,
            path,
            uriUsername: Server.Info.Username,
            useServerUsername: false);
        var sourceClient = sourceProvider.ClientAt(sourceProvider.Uri);
        var sourceItems = await List(sourceClient, Token);
        var info = sourceItems.Single(item => item.Name == name);
        var provided = await sourceProvider.Provide(new FtpEntry(sourceProvider, info));

        var copied = await targetProvider.Copy(provided, Token);

        Assert.Multiple(() => {
            Assert.That(copied, Is.False);
            Assert.That(File.ReadAllText(localFile), Is.EqualTo(original));
        });
    }

    [TestCase("ftp")]
    [TestCase("ftpes")]
    public async Task EnsureDirectory_AcceptsDirectoryLinksOnlyAfterServerCwdSucceeds(string scheme) {
        if (!Server.Info.SymlinksAvailable) {
            Assert.Ignore("The FTP test server could not create its directory-link fixture.");
            return;
        }
        var port = scheme == "ftp" ? Server.Info.FtpPort : Server.Info.ExplicitFtpsPort;
        using var provider = await CopyProvider(scheme, port, Server.Info.FixturePath + "/");
        var name = "copy-through-directory-link-" + Guid.NewGuid().ToString("N") + ".txt";
        var input = CopyInput(new TestStreamSource("nested-link/" + name, "directory link destination\n"));
        Assert.That(await provider.Copy([input], Token), Is.True);
        var linkTargetFile = Path.Combine(Server.Info.RootPath, "fixtures", "nested", name);
        Assert.That(File.ReadAllText(linkTargetFile), Is.EqualTo("directory link destination\n"));
    }

    [TestCase("ftp")]
    [TestCase("ftpes")]
    [TestCase("ftps")]
    public async Task Open_ResolvesSyntheticLinkMetadataAgainstRealTargets(string scheme) {
        var port = scheme switch {
            "ftp" => Server.Info.FtpPort,
            "ftpes" => Server.Info.ExplicitFtpsPort,
            "ftps" => Server.Info.ImplicitFtpsPort,
            _ => throw new ArgumentOutOfRangeException(nameof(scheme))
        };
        using var provider = await CopyProvider(scheme, port, Server.Info.FixturePath + "/");
        var opener = new CapturingOpenFile();
        var command = OpenCommand(opener);
        var fileEntry = SyntheticLink(provider, "alpha.txt", linkLength: 3);
        var filePanel = new LiveOpenPanel(provider);
        var fileContext = OpenContext(filePanel, fileEntry);

        Assert.That(((ICommand)command).TriggeredWork(fileContext), Is.True);
        var openedPath = opener.Path;

        try {
            Assert.Multiple(() => {
                Assert.That(File.ReadAllText(openedPath), Is.EqualTo("alpha fixture\n"));
                Assert.That(new FileInfo(openedPath).Length, Is.GreaterThan(fileEntry.Info.Size));
                Assert.That(File.GetAttributes(openedPath).HasFlag(FileAttributes.ReadOnly), Is.True);
            });

            var directoryEntry = SyntheticLink(provider, "nested", linkLength: 6);
            var directoryPanel = new LiveOpenPanel(provider);
            var directoryContext = OpenContext(directoryPanel, directoryEntry);
            Assert.That(((ICommand)command).TriggeredWork(directoryContext), Is.True);
            Assert.Multiple(() => {
                Assert.That(directoryPanel.ProvidedIDs, Has.Count.EqualTo(1));
                Assert.That(new Uri(directoryPanel.ProvidedIDs.Single()).AbsolutePath, Is.EqualTo("/fixtures/nested/"));
                Assert.That(directoryPanel.NavigatedNames, Does.Contain("child.txt"));
            });

            var temporaryDirectory = Path.Combine(Path.GetTempPath(), "Brows", "Ftp");
            var existingFiles = Directory.GetFiles(temporaryDirectory);
            var brokenName = "missing-open-link-" + Guid.NewGuid().ToString("N");
            var brokenEntry = SyntheticLink(provider, brokenName, linkLength: 4);
            var brokenContext = OpenContext(new LiveOpenPanel(provider), brokenEntry);
            var failure = Assert.Catch<Exception>(() => ((ICommand)command).TriggeredWork(brokenContext));
            Assert.Multiple(() => {
                Assert.That(failure, Is.Not.Null);
                Assert.That(Directory.GetFiles(temporaryDirectory), Is.EquivalentTo(existingFiles));
            });
        }
        finally {
            RemoveOpenedFile(openedPath);
        }
    }

    [TestCase("ftp")]
    [TestCase("ftpes")]
    public async Task Open_UsesActualSymlinkFixturesWhenTheHostCanCreateThem(string scheme) {
        if (!Server.Info.SymlinksAvailable) {
            Assert.Ignore("The FTP test server could not create its symlink fixtures on this host.");
            return;
        }
        var port = scheme == "ftp" ? Server.Info.FtpPort : Server.Info.ExplicitFtpsPort;
        using var provider = await CopyProvider(scheme, port, Server.Info.FixturePath + "/");
        var listing = await List(provider.ClientAt(provider.Uri), Token);
        var linkInfo = listing.Single(item => item.Name == "alpha-link.txt");
        var link = new FtpEntry(provider, linkInfo);
        var opener = new CapturingOpenFile();
        var command = OpenCommand(opener);
        var context = OpenContext(new LiveOpenPanel(provider), link);

        try {
            Assert.That(((ICommand)command).TriggeredWork(context), Is.True);
            Assert.That(File.ReadAllText(opener.Path), Is.EqualTo("alpha fixture\n"));

            var directoryLinkInfo = listing.Single(item => item.Name == "nested-link");
            var directoryLink = new FtpEntry(provider, directoryLinkInfo);
            var directoryPanel = new LiveOpenPanel(provider);
            var directoryContext = OpenContext(directoryPanel, directoryLink);
            Assert.That(((ICommand)command).TriggeredWork(directoryContext), Is.True);
            Assert.Multiple(() => {
                var navigationUri = new Uri(directoryPanel.ProvidedIDs.Single());
                Assert.That(navigationUri.AbsolutePath, Is.EqualTo("/fixtures/nested-link/"));
                Assert.That(directoryPanel.NavigatedNames, Does.Contain("child.txt"));
            });
        }
        finally {
            RemoveOpenedFile(opener.Path);
        }
    }

    [Test]
    public async Task DirectoryProbe_PropagatesAuthenticationTlsNetworkAndCancellationFailures() {
        var ftp = await Client("ftp", Server.Info.FtpPort);
        var fileIsDirectory = await ftp.IsDirectory(Server.Info.FixturePath + "/alpha.txt", Token);
        Assert.That(fileIsDirectory, Is.False);

        var denied = await Client("ftp", Server.Info.FtpPort, password: "incorrect-test-password");
        await Assert.CatchAsync<IOException>(async () => {
            _ = await denied.IsDirectory(Server.Info.FixturePath + "/alpha.txt", Token);
        });

        var untrusted = await Client("ftpes", Server.Info.ExplicitFtpsPort);
        await Assert.CatchAsync<IOException>(async () => {
            _ = await untrusted.IsDirectory(Server.Info.FixturePath + "/alpha.txt", Token);
        });

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var unusedPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var unreachable = new FtpClient {
            Uri = new UriBuilder("ftp", "127.0.0.1", unusedPort, "/").Uri,
            Resolve = false
        };
        await Assert.CatchAsync<IOException>(async () => {
            _ = await unreachable.IsDirectory("/missing", Token);
        });

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.CatchAsync<OperationCanceledException>(async () => {
            _ = await ftp.IsDirectory(Server.Info.FixturePath + "/alpha.txt", cancellation.Token);
        });
    }

    private static FtpEntry SyntheticLink(TestProvider provider, string name, long linkLength) {
        var info = new DirectoryListItem(name, linkLength, null, null, null, null, DirectoryListItemFlags.Link);
        var uri = FileProtocolUri.Extend(provider.Uri, [name], includeTrailingSlash: false);
        return new FtpEntry(provider, info, uri);
    }

    private static TestOpenCommand OpenCommand(CapturingOpenFile opener) {
        var command = new TestOpenCommand { OpenFile = opener };
        InitializeCommand(command);
        return command;
    }

    private static void InitializeCommand(ICommand command) {
        var framework = typeof(ICommand).Assembly;
        var cliProviderType = framework.GetType("Brows.Commands.CommandCliProvider", throwOnError: true);
        var cliProvider = Activator.CreateInstance(cliProviderType, nonPublic: true);
        var property = framework.GetType("Brows.Commands.Command", throwOnError: true)
            .GetProperty("CommandCliProvider", BindingFlags.Instance | BindingFlags.NonPublic);
        property.SetValue(command, cliProvider);
    }

    private static TestCommandContext OpenContext(LiveOpenPanel panel, FtpEntry entry) {
        var panels = new TestPanelCollection();
        panels.Panels.Add(panel);
        return new TestCommandContext {
            Panels = panels,
            Source = new TestCommandSource { Items = { entry } },
            Line = new TestCommandLine()
        };
    }

    private static void RemoveOpenedFile(string path) {
        if (path is null || !File.Exists(path)) {
            return;
        }
        File.SetAttributes(path, FileAttributes.Normal);
        File.Delete(path);
    }

    [TestCase("ftp")]
    [TestCase("ftpes")]
    public async Task Copy_RejectsSourceDirectoryLinksWithoutTraversingThem(string scheme) {
        if (!Server.Info.SymlinksAvailable) {
            Assert.Ignore("The FTP test server could not create its directory-link fixture.");
            return;
        }
        var port = scheme == "ftp" ? Server.Info.FtpPort : Server.Info.ExplicitFtpsPort;
        using var sourceProvider = await CopyProvider(
            scheme,
            port,
            Server.Info.FixturePath + "/");
        var items = await List(sourceProvider.ClientAt(sourceProvider.Uri), Token);
        var linkInfo = items.Single(item => item.Name == "nested-link");
        var linkEntry = new FtpEntry(sourceProvider, linkInfo);
        Assert.That(linkEntry.Kind, Is.AnyOf(FileProtocolEntryKind.FileLink, FileProtocolEntryKind.DirectoryLink));
        var provided = await sourceProvider.Provide(linkEntry);

        var targetName = "copy-link-reject-" + Guid.NewGuid().ToString("N");
        var targetDirectory = Path.Combine(Server.Info.RootPath, "fixtures", targetName);
        using var targetProvider = await CopyProvider(
            scheme,
            port,
            Server.Info.FixturePath + "/" + targetName + "/");
        var failure = await Failure(async () => {
            _ = await targetProvider.Copy(provided, Token);
        });
        Assert.That(failure, Is.InstanceOf<NotSupportedException>());
        Assert.That(Directory.Exists(targetDirectory), Is.False);
    }

    [TestCase("ftp")]
    [TestCase("ftpes")]
    public async Task MoveProvidedIO_MovesLocalFilesAfterTheFtpUploadCompletes(string scheme) {
        var port = scheme == "ftp" ? Server.Info.FtpPort : Server.Info.ExplicitFtpsPort;
        var targetName = "move-local-" + Guid.NewGuid().ToString("N") + ".txt";
        var sourceDirectory = Path.Combine(Path.GetTempPath(), "brows-ftp-move-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sourceDirectory);
        var sourcePath = Path.Combine(sourceDirectory, targetName);
        var targetPath = Path.Combine(Server.Info.RootPath, "fixtures", targetName);
        File.WriteAllText(sourcePath, "local FTP move fixture\n");

        try {
            using var provider = await CopyProvider(scheme, port, Server.Info.FixturePath + "/");
            var input = new ProvidedIO { StreamSets = [new LocalFileStreamSet(sourcePath)] };

            Assert.That(await provider.MoveLocal([input], Token), Is.True);

            Assert.Multiple(() => {
                Assert.That(File.Exists(sourcePath), Is.False);
                Assert.That(File.ReadAllText(targetPath), Is.EqualTo("local FTP move fixture\n"));
            });
        }
        finally {
            if (File.Exists(sourcePath)) {
                File.Delete(sourcePath);
            }
            if (Directory.Exists(sourceDirectory)) {
                Directory.Delete(sourceDirectory, recursive: false);
            }
            if (File.Exists(targetPath)) {
                File.Delete(targetPath);
            }
        }
    }

    [Test]
    public async Task MoveProvidedIO_RetainsLocalSourceWhenTheFtpServerRejectsTheUpload() {
        var sourceDirectory = Path.Combine(Path.GetTempPath(), "brows-ftp-move-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sourceDirectory);
        var sourcePath = Path.Combine(sourceDirectory, "empty");
        File.WriteAllText(sourcePath, "the server must reject this upload\n");

        try {
            using var provider = await CopyProvider("ftp", Server.Info.FtpPort, Server.Info.FixturePath + "/");
            var input = new ProvidedIO { StreamSets = [new LocalFileStreamSet(sourcePath)] };
            var failure = await Failure(async () => {
                _ = await provider.MoveLocal([input], Token);
            });

            Assert.That(failure, Is.Not.Null, "The FTP server must reject STOR to its existing directory.");
            Assert.That(File.Exists(sourcePath), Is.True);
        }
        finally {
            if (File.Exists(sourcePath)) {
                File.Delete(sourcePath);
            }
            if (Directory.Exists(sourceDirectory)) {
                Directory.Delete(sourceDirectory, recursive: false);
            }
        }
    }

    [TestCase("ftp")]
    [TestCase("ftpes")]
    [TestCase("ftps")]
    public async Task Move_FailedDownloadRetainsRemoteSourceAndDoesNotPublishPartialFile(string scheme) {
        var name = "brows-failed-download-" + Guid.NewGuid().ToString("N") + ".txt";
        var sourcePath = Path.Combine(Server.Info.RootPath, "fixtures", name);
        const string contents = "The complete remote file must survive a failed download.";
        File.WriteAllText(sourcePath, contents);
        var port = scheme switch {
            "ftp" => Server.Info.FtpPort,
            "ftpes" => Server.Info.ExplicitFtpsPort,
            _ => Server.Info.ImplicitFtpsPort
        };
        using var source = await CopyProvider(scheme, port, Server.Info.FixturePath + "/");
        var items = await List(source.ClientAt(source.Uri), Token);
        var entry = new FtpEntry(source, items.Single(item => item.Name == name));
        var targetPath = Path.Combine(Server.Info.RootPath, "failed-download-" + Guid.NewGuid().ToString("N"));
        using var target = new FileSystemTestDestination(targetPath);

        Assert.Catch<Exception>(() => Move(source, target.Provider, target.Copy, entry));

        Assert.That(File.ReadAllText(sourcePath), Is.EqualTo(contents));
        Assert.That(Directory.GetFileSystemEntries(targetPath), Is.Empty);
    }

    [Test]
    public async Task Copy_DirectoryToFilesystemRetriesLoginsRefusedBySessionLimit() {
        var directoryName = "session-limited-" + Guid.NewGuid().ToString("N");
        var sourceDirectory = Path.Combine(Server.Info.RootPath, "fixtures", directoryName);
        var targetPath = Path.Combine(Server.Info.RootPath, "session-limited-target-" + Guid.NewGuid().ToString("N"));
        var expected = new Dictionary<string, string>(StringComparer.Ordinal);
        Directory.CreateDirectory(Path.Combine(sourceDirectory, "nested"));
        for (var i = 0; i < 5; i++) {
            expected[$"file{i}.txt"] = new string((char)('a' + i), 16384);
        }
        for (var i = 0; i < 2; i++) {
            expected[Path.Combine("nested", $"inner{i}.txt")] = new string((char)('p' + i), 16384);
        }
        foreach (var item in expected) {
            File.WriteAllText(Path.Combine(sourceDirectory, item.Key), item.Value);
        }

        try {
            using var source = await CopyProvider(
                "ftp",
                Server.Info.FtpSessionLimitedPort,
                Server.Info.FixturePath + "/");
            var items = await List(source.ClientAt(source.Uri), Token);
            var entry = new FtpEntry(source, items.Single(item => item.Name == directoryName));
            var provided = await source.Provide(entry);
            using var target = new FileSystemTestDestination(targetPath);

            var copied = await target.Copy.Work(provided, target.Provider, progress: null, Token);

            var copiedDirectory = Path.Combine(targetPath, directoryName);
            using (Assert.EnterMultipleScope()) {
                Assert.That(copied, Is.True);
                foreach (var item in expected) {
                    Assert.That(File.ReadAllText(Path.Combine(copiedDirectory, item.Key)), Is.EqualTo(item.Value));
                }
                Assert.That(Directory.GetFiles(targetPath, ".brows-*.tmp", SearchOption.AllDirectories), Is.Empty);
                Assert.That(source.Transfers.Limit, Is.EqualTo(Server.Info.FtpSessionLimit));
            }
        }
        finally {
            if (Directory.Exists(sourceDirectory)) {
                Directory.Delete(sourceDirectory, recursive: true);
            }
        }
    }

    [Test]
    public async Task Move_FtpToFilesystemCopiesTheFileBeforeDeletingTheRemoteSource() {
        var name = "move-to-local-" + Guid.NewGuid().ToString("N") + ".txt";
        var sourcePath = Path.Combine(Server.Info.RootPath, "fixtures", name);
        var targetDirectory = Path.Combine(Path.GetTempPath(), "brows-ftp-target-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(sourcePath, "remote FTP move fixture\n");

        try {
            using var sourceProvider = await CopyProvider("ftp", Server.Info.FtpPort, Server.Info.FixturePath + "/");
            var items = await List(sourceProvider.ClientAt(sourceProvider.Uri), Token);
            var info = items.Single(item => item.Name == name);
            var entry = new FtpEntry(sourceProvider, info);
            using var targetProvider = new TestProvider(new Uri(targetDirectory + Path.DirectorySeparatorChar));
            var copy = new LocalCopyIO(targetDirectory);
            var worked = Move(sourceProvider, targetProvider, copy, entry);

            Assert.Multiple(() => {
                Assert.That(worked, Is.True);
                Assert.That(File.Exists(sourcePath), Is.False);
                Assert.That(File.ReadAllText(Path.Combine(targetDirectory, name)),
                    Is.EqualTo("remote FTP move fixture\n"));
            });
        }
        finally {
            if (File.Exists(sourcePath)) {
                File.Delete(sourcePath);
            }
            if (Directory.Exists(targetDirectory)) {
                Directory.Delete(targetDirectory, recursive: true);
            }
        }
    }

    [Test]
    public async Task Move_CopiesAcrossFtpEndpointsBeforeDeletingTheRemoteSource() {
        var name = "move-across-endpoints-" + Guid.NewGuid().ToString("N") + ".txt";
        var targetName = "move-cross-target-" + Guid.NewGuid().ToString("N");
        var sourcePath = Path.Combine(Server.Info.RootPath, "home", "fixtures", name);
        var targetDirectory = Path.Combine(Server.Info.RootPath, "home", "fixtures", targetName);
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(sourcePath, "cross endpoint FTP move fixture\n");

        try {
            using var sourceProvider = await CopyProvider(
                "ftp",
                Server.Info.FtpPort,
                Server.Info.FixturePath + "/",
                Server.Info.LoginHomeUsername);
            using var targetProvider = await CopyProvider(
                "ftpes",
                Server.Info.ExplicitFtpsPort,
                Server.Info.FixturePath + "/" + targetName + "/",
                Server.Info.LoginHomeUsername);
            var items = await List(sourceProvider.ClientAt(sourceProvider.Uri), Token);
            var info = items.Single(item => item.Name == name);
            var entry = new FtpEntry(sourceProvider, info);
            var worked = Move(sourceProvider, targetProvider, targetProvider.CopyIOExport, entry);

            Assert.Multiple(() => {
                Assert.That(worked, Is.True);
                Assert.That(File.Exists(sourcePath), Is.False);
                Assert.That(File.ReadAllText(Path.Combine(targetDirectory, name)),
                    Is.EqualTo("cross endpoint FTP move fixture\n"));
            });
        }
        finally {
            if (File.Exists(sourcePath)) {
                File.Delete(sourcePath);
            }
            if (Directory.Exists(targetDirectory)) {
                Directory.Delete(targetDirectory, recursive: true);
            }
        }
    }

    [TestCase("ftp")]
    [TestCase("ftpes")]
    public async Task CreateDirectory_StillRejectsAnExistingDirectory(string scheme) {
        var port = scheme == "ftp" ? Server.Info.FtpPort : Server.Info.ExplicitFtpsPort;
        var name = "create-existing-directory-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Path.Combine(Server.Info.RootPath, "fixtures", name));
        var client = await Client(scheme, port, scheme == "ftp" ? null : Server.Info.CaPath);

        await AssertFails(() => client.CreateDirectory(Server.Info.FixturePath + "/" + name, Token));
    }

    [Test]
    public async Task PlainFtp_LoginHomePathsListTransferAndMutateTheSameObjects() {
        var loginHomeUsername = Server.Info.LoginHomeUsername;
        var client = await Client(
            "ftp",
            Server.Info.FtpPort,
            path: Server.Info.FixturePath + '/',
            username: loginHomeUsername);
        var items = await List(client, Token);
        var itemNames = items.Select(item => item.Name).ToArray();

        var fileClient = await Client(
            "ftp",
            Server.Info.FtpPort,
            path: Server.Info.FixturePath + "/alpha.txt",
            username: loginHomeUsername);
        var downloaded = Encoding.UTF8.GetString(await Download(fileClient, Token));

        var uploadPath = Server.Info.FixturePath + "/uploaded from login home.txt";
        var upload = Encoding.UTF8.GetBytes("login-home upload fixture\n");
        await using (var stream = await client.Write(uploadPath, upload.Length, Token)) {
            await stream.WriteAsync(upload, Token);
        }
        await client.CreateDirectory(Server.Info.FixturePath + "/created from login home", Token);
        await client.Delete(Server.Info.FixturePath + "/delete.txt", directory: false, Token);
        await client.Delete(Server.Info.FixturePath + "/remove-dir", directory: true, Token);
        await client.Rename(
            Server.Info.FixturePath + "/rename.txt",
            Server.Info.FixturePath + "/renamed.txt",
            Token);

        var rootFixtures = Path.Combine(Server.Info.RootPath, "fixtures");
        var loginHomeFixtures = Path.Combine(Server.Info.RootPath, "home", "fixtures");
        Assert.Multiple(() => {
            Assert.That(itemNames, Does.Contain("login-home-only.txt"));
            Assert.That(itemNames, Does.Contain("alpha.txt"));
            Assert.That(itemNames, Does.Not.Contain("server-root-only.txt"));
            Assert.That(downloaded, Is.EqualTo("login-home alpha fixture\n"));
            Assert.That(
                File.ReadAllText(Path.Combine(loginHomeFixtures, "uploaded from login home.txt")),
                Is.EqualTo("login-home upload fixture\n"));
            Assert.That(File.Exists(Path.Combine(rootFixtures, "uploaded from login home.txt")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(loginHomeFixtures, "created from login home")), Is.True);
            Assert.That(Directory.Exists(Path.Combine(rootFixtures, "created from login home")), Is.False);
            Assert.That(File.Exists(Path.Combine(loginHomeFixtures, "delete.txt")), Is.False);
            Assert.That(File.Exists(Path.Combine(rootFixtures, "delete.txt")), Is.True);
            Assert.That(Directory.Exists(Path.Combine(loginHomeFixtures, "remove-dir")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(rootFixtures, "remove-dir")), Is.True);
            Assert.That(File.Exists(Path.Combine(loginHomeFixtures, "rename.txt")), Is.False);
            Assert.That(
                File.ReadAllText(Path.Combine(loginHomeFixtures, "renamed.txt")),
                Is.EqualTo("login-home rename fixture\n"));
            Assert.That(File.Exists(Path.Combine(rootFixtures, "rename.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(rootFixtures, "renamed.txt")), Is.False);
        });
    }

    [Test]
    public async Task PlainFtp_DoubleSlashPathsStayServerAbsolute() {
        var client = await Client(
            "ftp",
            Server.Info.FtpPort,
            path: "//fixtures/",
            username: Server.Info.LoginHomeUsername);
        var items = await List(client, Token);
        var itemNames = items.Select(item => item.Name).ToArray();

        var fileClient = await Client(
            "ftp",
            Server.Info.FtpPort,
            path: "//fixtures/alpha.txt",
            username: Server.Info.LoginHomeUsername);
        var downloaded = Encoding.UTF8.GetString(await Download(fileClient, Token));

        var uploadPath = "//fixtures/absolute-upload.txt";
        var upload = Encoding.UTF8.GetBytes("server-absolute upload fixture\n");
        await using (var stream = await client.Write(uploadPath, upload.Length, Token)) {
            await stream.WriteAsync(upload, Token);
        }
        await client.CreateDirectory("//fixtures/absolute-created", Token);
        await client.Delete("//fixtures/absolute-delete.txt", directory: false, Token);
        await client.Delete("//fixtures/absolute-remove-dir", directory: true, Token);
        await client.Rename(
            "//fixtures/absolute-rename.txt",
            "//fixtures/absolute-renamed.txt",
            Token);

        var rootFixtures = Path.Combine(Server.Info.RootPath, "fixtures");
        var loginHomeFixtures = Path.Combine(Server.Info.RootPath, "home", "fixtures");
        Assert.Multiple(() => {
            Assert.That(itemNames, Does.Contain("server-root-only.txt"));
            Assert.That(itemNames, Does.Not.Contain("login-home-only.txt"));
            Assert.That(downloaded, Is.EqualTo("alpha fixture\n"));
            Assert.That(File.Exists(Path.Combine(rootFixtures, "absolute-upload.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(loginHomeFixtures, "absolute-upload.txt")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(rootFixtures, "absolute-created")), Is.True);
            Assert.That(Directory.Exists(Path.Combine(loginHomeFixtures, "absolute-created")), Is.False);
            Assert.That(File.Exists(Path.Combine(rootFixtures, "absolute-delete.txt")), Is.False);
            Assert.That(File.Exists(Path.Combine(loginHomeFixtures, "absolute-delete.txt")), Is.True);
            Assert.That(Directory.Exists(Path.Combine(rootFixtures, "absolute-remove-dir")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(loginHomeFixtures, "absolute-remove-dir")), Is.True);
            Assert.That(File.Exists(Path.Combine(rootFixtures, "absolute-rename.txt")), Is.False);
            Assert.That(
                File.ReadAllText(Path.Combine(rootFixtures, "absolute-renamed.txt")),
                Is.EqualTo("server-root absolute rename fixture\n"));
            Assert.That(File.Exists(Path.Combine(loginHomeFixtures, "absolute-rename.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(loginHomeFixtures, "absolute-renamed.txt")), Is.False);
        });
    }

    [Test]
    public async Task PlainFtp_CommandPathsRejectCarriageReturnAndLineFeed() {
        var client = await Client(
            "ftp",
            Server.Info.FtpPort,
            path: Server.Info.FixturePath + '/',
            username: Server.Info.LoginHomeUsername);
        foreach (var lineBreak in new[] { "\r", "\n", "\r\n" }) {
            var path = Server.Info.FixturePath + "/injected" + lineBreak + "DELE alpha.txt";
            await AssertFails(() => client.CreateDirectory(path, Token));
        }

        var loginHomeAlpha = Path.Combine(Server.Info.RootPath, "home", "fixtures", "alpha.txt");
        Assert.That(File.Exists(loginHomeAlpha), Is.True);
        Assert.That(Directory.Exists(Path.Combine(Server.Info.RootPath, "home", "fixtures", "injected")), Is.False);
    }

    [Test]
    public async Task ImplicitFtps_ListAndDownloadWithTrustedCertificate() {
        var client = await Client("ftps", Server.Info.ImplicitFtpsPort, Server.Info.CaPath);
        var items = await List(client, Token);
        Assert.That(items.Select(item => item.Name), Does.Contain("alpha.txt"));
        var fileClient = await Client(
            "ftps",
            Server.Info.ImplicitFtpsPort,
            Server.Info.CaPath,
            path: Server.Info.FixturePath + "/alpha.txt");
        Assert.That(Encoding.UTF8.GetString(await Download(fileClient, Token)), Is.EqualTo("alpha fixture\n"));
    }

    [TestCase("ftps", "default")]
    [TestCase("ftps", "true")]
    [TestCase("ftpes", "default")]
    [TestCase("ftpes", "true")]
    public async Task Ftps_PreservesDnsHostnameForTrustedDnsOnlyCertificate(string scheme, string resolution) {
        var isImplicit = scheme.Equals("ftps", StringComparison.OrdinalIgnoreCase);
        var port = isImplicit
            ? Server.Info.ImplicitFtpsDnsOnlyPort
            : Server.Info.ExplicitFtpsDnsOnlyPort;
        bool? resolve = resolution == "default" ? null : true;
        var client = await Client(
            scheme,
            port,
            Server.Info.CaPath,
            host: "localhost",
            resolve: resolve);
        var items = await List(client, Token);

        Assert.Multiple(() => {
            Assert.That(client.Resolve, Is.True);
            Assert.That(items.Select(item => item.Name), Does.Contain("alpha.txt"));
        });
    }

    [Test]
    public async Task ImplicitFtps_ConfiguredNonePreservesDnsHostnameAndTrustedCertificate() {
        var client = await Client(
            "ftps",
            Server.Info.ImplicitFtpsDnsOnlyPort,
            Server.Info.CaPath,
            host: "localhost",
            resolve: null);
        client.TlsMode = FtpTlsMode.None;
        var items = await List(client, Token);

        Assert.Multiple(() => {
            Assert.That(client.Resolve, Is.True);
            Assert.That(client.EffectiveTlsMode, Is.EqualTo(FtpTlsMode.Implicit));
            Assert.That(items.Select(item => item.Name), Does.Contain("alpha.txt"));
        });
    }

    [TestCase("Auto")]
    [TestCase("None")]
    public async Task ImplicitFtps_FailsWhenServerRejectsDataTls(string configuredTlsMode) {
        var port = Server.Info.ImplicitFtpsRejectDataTlsPort;
        var tlsMode = Enum.Parse<FtpTlsMode>(configuredTlsMode);
        var client = await Client("ftps", port, Server.Info.CaPath);
        client.TlsMode = tlsMode;
        var listingFailure = await Failure(async () => await List(client, Token));

        var fileClient = await Client(
            "ftps",
            port,
            Server.Info.CaPath,
            path: Server.Info.FixturePath + "/alpha.txt");
        fileClient.TlsMode = tlsMode;
        var downloadFailure = await Failure(async () => await Download(fileClient, Token));

        var upload = Encoding.UTF8.GetBytes("implicit TLS upload must fail\n");
        var uploadFailure = await Failure(async () => {
            await using var stream = await client.Write(
                Server.Info.FixturePath + "/implicit tls upload.txt",
                upload.Length,
                Token);
            await stream.WriteAsync(upload, Token);
        });

        Assert.Multiple(() => {
            Assert.That(listingFailure, Is.Not.Null, "A listing must fail when data TLS is refused.");
            Assert.That(downloadFailure, Is.Not.Null, "A download must fail when data TLS is refused.");
            Assert.That(uploadFailure, Is.Not.Null, "An upload must fail when data TLS is refused.");
            var uploadPath = Path.Combine(Server.Info.RootPath, "fixtures", "implicit tls upload.txt");
            Assert.That(File.Exists(uploadPath), Is.False, "A refused upload must not create a remote file.");
        });
    }

    [Test]
    public async Task NativeImplicitFtps_NoneModeCannotDisableDataTls() {
        var uri = new UriBuilder(
            "ftps",
            Server.Info.Host,
            Server.Info.ImplicitFtpsRejectDataTlsPort,
            Server.Info.FixturePath.TrimEnd('/') + '/')
            .Uri;
        using var client = new ClientForUrl(uri);
        client.OnWrite(static _ => { });
        client.Username(Server.Info.Username);
        client.Password(Server.Info.Password);
        client.FtpTls(FtpNativeTlsMode.None);
        client.FtpCertificateAuthorityFile(Server.Info.CaPath);

        var failure = await Failure(() => Task.Run(client.TxRx));
        Assert.That(failure, Is.Not.Null, "The native adapter must not use clear data for an ftps:// URL.");
    }

    [Test]
    public async Task ExplicitFtps_ListAndUploadWithTrustedCertificate() {
        var client = await Client("ftpes", Server.Info.ExplicitFtpsPort, Server.Info.CaPath);
        Assert.That(client.EffectiveTlsMode, Is.EqualTo(FtpTlsMode.Explicit));
        var items = await List(client, Token);
        Assert.That(items.Select(item => item.Name), Does.Contain("alpha.txt"));

        var uploadPath = Server.Info.FixturePath + "/explicit tls upload.txt";
        var upload = Encoding.UTF8.GetBytes("explicit TLS upload\n");
        await using (var stream = await client.Write(uploadPath, upload.Length, Token)) {
            await stream.WriteAsync(upload, Token);
        }
        items = await List(client, Token);
        Assert.That(items.Select(item => item.Name), Does.Contain("explicit tls upload.txt"));
        await client.Delete(uploadPath, directory: false, Token);
    }

    [TestCase("ftp")]
    [TestCase("ftpes")]
    [TestCase("ftps")]
    public async Task CreateFile_RefusesExistingNamesAndPreservesFilesThatAppearAfterPreflight(string scheme) {
        var port = scheme switch {
            "ftp" => Server.Info.FtpPort,
            "ftpes" => Server.Info.ExplicitFtpsPort,
            "ftps" => Server.Info.ImplicitFtpsPort,
            _ => throw new ArgumentOutOfRangeException(nameof(scheme))
        };
        var caPath = scheme == "ftp" ? null : Server.Info.CaPath;
        var client = await Client(scheme, port, caPath);
        var fixtureDirectory = Path.Combine(Server.Info.RootPath, "fixtures");

        var freshName = "create-empty-" + Guid.NewGuid().ToString("N") + ".txt";
        var freshPath = Server.Info.FixturePath + "/" + freshName;
        await client.CreateFile(freshPath, freshName, Token);
        Assert.Multiple(() => {
            Assert.That(File.Exists(Path.Combine(fixtureDirectory, freshName)), Is.True);
            Assert.That(new FileInfo(Path.Combine(fixtureDirectory, freshName)).Length, Is.Zero);
        });

        var existingName = "create-existing-" + Guid.NewGuid().ToString("N") + ".txt";
        var existingPath = Server.Info.FixturePath + "/" + existingName;
        var originalContents = Encoding.UTF8.GetBytes("existing remote contents survive create\n");
        await using (var stream = await client.Write(existingPath, originalContents.Length, Token)) {
            await stream.WriteAsync(originalContents, Token);
        }
        await AssertFails(() => client.CreateFile(existingPath, existingName, Token));
        Assert.That(File.ReadAllBytes(Path.Combine(fixtureDirectory, existingName)), Is.EqualTo(originalContents));

        var overwriteContents = Encoding.UTF8.GetBytes("ordinary upload still overwrites\n");
        await using (var stream = await client.Write(existingPath, overwriteContents.Length, Token)) {
            await stream.WriteAsync(overwriteContents, Token);
        }
        Assert.That(File.ReadAllBytes(Path.Combine(fixtureDirectory, existingName)), Is.EqualTo(overwriteContents));

        var directoryName = "create-directory-collision-" + Guid.NewGuid().ToString("N");
        var directoryPath = Server.Info.FixturePath + "/" + directoryName;
        await client.CreateDirectory(directoryPath, Token);
        await AssertFails(() => client.CreateFile(directoryPath, directoryName, Token));
        Assert.That(Directory.Exists(Path.Combine(fixtureDirectory, directoryName)), Is.True);

        var raceName = "create-race-" + Guid.NewGuid().ToString("N") + ".txt";
        var racePath = Server.Info.FixturePath + "/" + raceName;
        var raceContents = Encoding.UTF8.GetBytes("created after the listing preflight\n");
        Server.CreateFileAfterNextListing(raceName, raceContents);
        await client.CreateFile(racePath, raceName, Token);
        Assert.That(Server.ListingRaceCompleted(raceName), Is.True);
        Assert.That(File.ReadAllBytes(Path.Combine(fixtureDirectory, raceName)), Is.EqualTo(raceContents));
    }

    [Test]
    public async Task ExplicitTlsCannotDowngradeAndRejectsUntrustedCertificateAndCredentials() {
        var noDowngrade = await Client("ftpes", Server.Info.FtpPort, Server.Info.CaPath);
        Assert.That(noDowngrade.EffectiveTlsMode, Is.EqualTo(FtpTlsMode.Explicit));
        await AssertFails(async () => await List(noDowngrade, Token));

        var untrusted = await Client("ftpes", Server.Info.ExplicitFtpsPort);
        await AssertFails(async () => await List(untrusted, Token));

        var badCredentials = await Client(
            "ftp",
            Server.Info.FtpPort,
            password: "incorrect-test-password");
        await AssertFails(async () => await List(badCredentials, Token));
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task Upload_FinalizesWithoutPumpingSynchronizationContext(bool asynchronous, bool reject) {
        var name = "context-upload-" + Guid.NewGuid().ToString("N") + ".bin";
        var uri = new UriBuilder("ftp", Server.Info.Host, Server.Info.FtpPort, "/fixtures/" + name).Uri;
        var context = new HoldingSynchronizationContext();
        var finished = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var thread = new Thread(() => {
            SynchronizationContext.SetSynchronizationContext(context);
            try {
                var native = new ClientForUrl(uri);
                native.Username(Server.Info.Username);
                native.Password(reject ? "incorrect" : Server.Info.Password);
                native.OnProgress(cancellation.Token);
                var type = typeof(UrlClient).Assembly.GetType("Brows.Url.IO.ClientUploadStream", true);
                var stream = (Stream)Activator.CreateInstance(type, [native, -1L, false, cancellation.Token]);
                try {
                    if (!reject) {
                        var bytes = new byte[65536];
                        for (var index = 0; index < 64; index++) {
                            stream.Write(bytes, 0, bytes.Length);
                        }
                    }
                }
                finally {
                    if (asynchronous) {
                        stream.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    }
                    else {
                        stream.Dispose();
                    }
                }
                finished.TrySetResult(null);
            }
            catch (Exception error) {
                finished.TrySetResult(error);
            }
            finally {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }) { IsBackground = true };
        thread.Start();
        var first = await Task.WhenAny(finished.Task, context.Posted.Task).WaitAsync(TimeSpan.FromSeconds(15));
        var neededPump = first == context.Posted.Task;
        try {
            if (neededPump) {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                while (!finished.Task.IsCompleted) {
                    context.Drain();
                    await Task.Delay(10, cleanup.Token);
                }
            }
            var failure = await finished.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.That(neededPump, Is.False, "Upload internals must not post to the caller's context.");
            if (reject) {
                Assert.That(failure, Is.Not.Null);
            }
            else {
                Assert.That(failure, Is.Null);
                Assert.That(new FileInfo(Path.Combine(Server.Info.RootPath, "fixtures", name)).Length,
                    Is.EqualTo(64 * 65536));
            }
        }
        finally {
            cancellation.Cancel();
            context.Drain();
            Assert.That(thread.Join(TimeSpan.FromSeconds(5)), Is.True);
        }
    }

    private sealed class HoldingSynchronizationContext : SynchronizationContext {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object State)> Queue = new();
        internal TaskCompletionSource Posted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void Post(SendOrPostCallback callback, object state) {
            Queue.Enqueue((callback, state));
            Posted.TrySetResult();
        }

        internal void Drain() {
            while (Queue.TryDequeue(out var item)) {
                item.Callback(item.State);
            }
        }
    }

    [Test]
    public async Task RejectedUploadUnblocksWriterAndDispose() {
        var client = await Client("ftp", Server.Info.FtpPort, password: "incorrect-test-password");
        var source = new byte[1024 * 1024];
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completed = false;
        try {
            await using var stream = await client.Write(
                Server.Info.FixturePath + "/rejected upload.bin",
                source.Length * 32L,
                cancellation.Token);
            for (var i = 0; i < 32; i++) {
                await stream.WriteAsync(source, cancellation.Token);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            completed = true;
        }
        Assert.That(cancellation.IsCancellationRequested, Is.False, "The rejected upload must finish before its timeout.");
        Assert.That(completed, Is.True, "The server rejection must unblock the upload writer or disposer.");
    }

    [Test]
    public async Task ExplicitFtpsSupportsIpv6WhenAvailable() {
        var port = Server.Info.ExplicitFtpsIpv6Port;
        if (port is null || string.IsNullOrWhiteSpace(Server.Info.Ipv6Host)) {
            Assert.Ignore("The host does not provide an IPv6 loopback listener.");
            return;
        }
        var client = await Client("ftpes", port.Value, Server.Info.CaPath, Server.Info.Ipv6Host);
        var items = await List(client, Token);
        Assert.That(items.Select(item => item.Name), Does.Contain("alpha.txt"));
    }

    [Test]
    public async Task ExplicitFtpsRejectsCertificateHostnameMismatch() {
        var client = await Client("ftpes", Server.Info.ExplicitFtpsPort, Server.Info.CaPath, "0.0.0.0");
        var failure = await Failure(async () => await List(client, Token));
        Assert.That(failure, Is.Not.Null, "The test certificate must fail when its hostname does not match.");
        var message = failure.ToString();
        var reportsCertificateMismatch =
            message.Contains("certificate", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("subject name", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("host name", StringComparison.OrdinalIgnoreCase);
        Assert.That(reportsCertificateMismatch, Is.True, message);
    }

    [Test]
    public async Task PlainFtp_TransfersPosixOnlyNamesWhenTheServerSupportsThem() {
        if (!Server.Info.PosixOnlyFileAvailable) {
            Assert.Ignore("The FTP test server cannot create POSIX-only filenames on this host.");
            return;
        }
        var client = await Client("ftp", Server.Info.FtpPort);
        var items = await List(client, Token);
        Assert.That(items.Select(item => item.Name), Does.Contain("colon:name.txt"));
        var fileClient = await Client(
            "ftp",
            Server.Info.FtpPort,
            path: Server.Info.FixturePath + "/colon:name.txt");
        Assert.That(
            Encoding.UTF8.GetString(await Download(fileClient, Token)),
            Is.EqualTo("POSIX filename fixture\n"));
        var uploadPath = Server.Info.FixturePath + "/colon:uploaded.txt";
        await using (var stream = await client.Write(uploadPath, 0, Token)) {
        }
        await client.Delete(uploadPath, directory: false, Token);
    }

    private sealed class LiveDeleteProvider : FtpProviderBase {
        private readonly FtpClient Client;

        public LiveDeleteProvider(Uri uri, FtpClient client) : base(new LiveDeleteFactory(), uri, null) {
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        internal override async IAsyncEnumerable<FtpEntry> ListEntries(
            Uri uri,
            [EnumeratorCancellation] CancellationToken token) {
            Client.Uri = uri;
            var relativePath = FileProtocolUri.RelativePath(Uri, uri);
            await foreach (var item in Client.List(token).WithCancellation(token)) {
                if (item.Name is "." or "..") {
                    continue;
                }
                var entryUri = FileProtocolUri.Extend(
                    Uri,
                    [relativePath, item.Name],
                    includeTrailingSlash: item.EntryKind() == FileProtocolEntryKind.Directory);
                yield return new FtpEntry(this, item, entryUri);
            }
        }

        internal override Task DeleteEntry(FtpEntry entry, bool directory, CancellationToken token) {
            Client.Uri = entry.Uri;
            return Client.Delete(entry.RemotePath, directory, token);
        }
    }

    private sealed class LiveDeleteFactory : IFtpProviderFactory {
        public FtpClientCache Client { get; } = new();
    }

    private sealed class TestProvider : FtpProviderBase {
        private sealed class ProvideExport : FtpProvideIO<TestProvider> {
        }

        private sealed class CopyExport : FtpCopyIO<TestProvider> {
        }

        private static readonly ProvideExport ProvideService = new();
        private static readonly CopyExport CopyService = new();
        private static readonly MoveExport MoveService = new();

        internal TestProvider(Uri uri) : base(new TestFactory(), uri, null) {
        }

        internal async Task<IReadOnlyList<IProvidedIO>> Provide(params FtpEntry[] entries) {
            var source = new TestCommandSource();
            foreach (var entry in entries) {
                source.Items.Add(entry);
            }
            var provided = new List<IProvidedIO>();
            var worked = await ((IProvideIO)ProvideService).Work(
                provided,
                source,
                this,
                progress: null,
                Token);
            if (!worked) {
                throw new InvalidOperationException("The FTP test provider did not prepare its source entries.");
            }
            return provided;
        }

        internal Task<bool> Copy(IEnumerable<IProvidedIO> io, CancellationToken token) {
            return ((IWorkProvidedIO)CopyService).Work(io, this, progress: null, token);
        }

        internal Task<bool> MoveLocal(IEnumerable<IProvidedIO> io, CancellationToken token) {
            return ((IWorkProvidedIO)MoveService).Work(io, this, progress: null, token);
        }

        internal IProviderExport ProvideIOExport => ProvideService;
        internal IProviderExport CopyIOExport => CopyService;

        private sealed class MoveExport : FtpMoveProvidedIO<TestProvider> {
            protected sealed override IWorkProvidedIO Copy => CopyService;
        }

        private sealed class TestFactory : IFtpProviderFactory {
            public FtpClientCache Client { get; } = new();
        }
    }

    private sealed class TestOpenCommand : FtpOpen<TestProvider> {
    }

    private sealed class CapturingOpenFile : IOpenFile {
        public string Path { get; private set; }

        public Task<bool> Work(string file, CancellationToken token) {
            Path = file;
            return Task.FromResult(true);
        }
    }

    private sealed class LiveOpenPanel : IPanel {
        private readonly TestPanel Panel;
        private readonly TestProvider Provider;

        public List<string> ProvidedIDs { get; } = [];
        public List<string> NavigatedNames { get; } = [];

        public int Column => Panel.Column;

        public LiveOpenPanel(TestProvider provider) {
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Panel = new TestPanel { Column = 0, Provider = provider };
        }

        public bool HasView(out IEntryDataView view) {
            return ((IPanel)Panel).HasView(out view);
        }

        public bool HasSelection(out IReadOnlySet<IEntry> entries) {
            return ((IPanel)Panel).HasSelection(out entries);
        }

        public bool HasSelection<TEntry>(out IReadOnlySet<TEntry> entries) where TEntry : class, IEntry {
            return ((IPanel)Panel).HasSelection(out entries);
        }

        public bool HasEntry(out IEntry current) {
            return ((IPanel)Panel).HasEntry(out current);
        }

        public bool HasEntries(out IReadOnlyList<IEntry> entries) {
            return ((IPanel)Panel).HasEntries(out entries);
        }

        public bool HasEntries<TEntry>(out IReadOnlyList<TEntry> entries) where TEntry : class, IEntry {
            return ((IPanel)Panel).HasEntries(out entries);
        }

        public bool HasProvider(out IProvider provider) {
            return ((IPanel)Panel).HasProvider(out provider);
        }

        public bool HasProvider<TProvider>(out TProvider provider) where TProvider : class, IProvider {
            return ((IPanel)Panel).HasProvider(out provider);
        }

        public bool HasProviderService<TService>(out IProvider provider, out TService service)
        where TService : class, IProviderExport {
            return ((IPanel)Panel).HasProviderService(out provider, out service);
        }

        public bool HasWindow(out object native) {
            return ((IPanel)Panel).HasWindow(out native);
        }

        public async Task<bool> Provide(string id, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            ProvidedIDs.Add(id);
            await foreach (var entry in Provider.ListEntries(new Uri(id), token).WithCancellation(token)) {
                NavigatedNames.Add(entry.Name);
            }
            return true;
        }

        public Task<TSecret> GetSecret<TSecret>(
            string key,
            string promptFormat,
            IEnumerable<string> promptArgs,
            CancellationToken token)
        where TSecret : IPanelSecret {
            return ((IPanel)Panel).GetSecret<TSecret>(key, promptFormat, promptArgs, token);
        }
    }

    private sealed class TestStreamSet : IEntryStreamSet {
        private readonly IReadOnlyList<IEntryStreamSource> Sources;

        public TestStreamSet(params IEntryStreamSource[] sources) {
            Sources = sources ?? throw new ArgumentNullException(nameof(sources));
        }

        public IEntryStreamReady StreamSourceReady() {
            return null;
        }

        public IEnumerable<string> FileSource() {
            return Array.Empty<string>();
        }

        public async IAsyncEnumerable<IEntryStreamSource> StreamSource(
            [EnumeratorCancellation] CancellationToken token) {
            foreach (var source in Sources) {
                token.ThrowIfCancellationRequested();
                yield return source;
            }
            await Task.CompletedTask;
        }
    }

    private sealed class LocalFileStreamSet : IEntryStreamSet {
        private readonly string Source;

        public LocalFileStreamSet(string source) {
            Source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public IEntryStreamReady StreamSourceReady() {
            return null;
        }

        public IEnumerable<string> FileSource() {
            return [Source];
        }

        public IAsyncEnumerable<IEntryStreamSource> StreamSource(CancellationToken token) {
            return null;
        }
    }

    private sealed class LocalCopyIO : ICopyProvidedIO {
        private readonly string Root;

        public LocalCopyIO(string root) {
            Root = Path.GetFullPath(root ?? throw new ArgumentNullException(nameof(root)));
            Directory.CreateDirectory(Root);
        }

        public async Task<bool> Work(
            IEnumerable<IProvidedIO> io,
            IProvider target,
            IOperationProgress progress,
            CancellationToken token) {
            var streamSets = io
                .SelectMany(item => item.StreamSets ?? Array.Empty<IEntryStreamSet>())
                .Where(set => set is not null)
                .ToList();
            var copied = false;
            foreach (var set in streamSets) {
                await set.Consume(async (source, stream, childProgress, childToken) => {
                    if (source is null || string.IsNullOrWhiteSpace(source.RelativePath)) {
                        return;
                    }
                    var relativePath = source.RelativePath.Replace('/', Path.DirectorySeparatorChar);
                    var destination = Path.GetFullPath(Path.Combine(Root, relativePath));
                    var rootPrefix = Path.EndsInDirectorySeparator(Root)
                        ? Root
                        : Root + Path.DirectorySeparatorChar;
                    if (!destination.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)) {
                        throw new InvalidDataException("The FTP entry path escapes the test destination.");
                    }
                    if (stream is null) {
                        if (source.SourceDirectory is not null) {
                            Directory.CreateDirectory(destination);
                            copied = true;
                        }
                        return;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    await using var output = File.Create(destination);
                    await stream.CopyToAsync(output, childToken);
                    copied = true;
                }, progress, token);
            }
            return copied;
        }
    }

    private sealed class LiveMoveCommand : FtpMove<TestProvider> {
        protected override Task RefreshProvider(IProvider provider, CancellationToken token) {
            return Task.CompletedTask;
        }
    }

    private sealed class TestStreamSource : IEntryStreamSource {
        private readonly byte[] Contents;

        public string EntryID { get; } = new Uri(
            "ftp://source.invalid/" + Guid.NewGuid().ToString("N") + "/").AbsoluteUri;
        public string EntryName => RelativePath.TrimEnd('/').Split('/').Last();
        public string RelativePath { get; }
        public string SourceFile => null;
        public string SourceDirectory => Contents is null ? RelativePath : null;
        public long StreamLength => Contents?.Length ?? 0;
        public Stream Stream { get; private set; }

        public TestStreamSource(string relativePath, string contents) {
            RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
            Contents = contents is null ? null : Encoding.UTF8.GetBytes(contents);
        }

        public Task<IEntryStreamReady> StreamReady(CancellationToken token) {
            token.ThrowIfCancellationRequested();
            if (Contents is not null) {
                Stream = new MemoryStream(Contents, writable: false);
            }
            return Task.FromResult<IEntryStreamReady>(new EntryStreamReady());
        }
    }
}
