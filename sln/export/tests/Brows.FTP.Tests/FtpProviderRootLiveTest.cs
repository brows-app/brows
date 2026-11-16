using Brows.DirectoryListing;
using Brows.Entries;
using Brows.Providers;
using Brows.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
[NonParallelizable]
public sealed class FtpProviderRootLiveTest {
    private static readonly CancellationToken Token = CancellationToken.None;
    private ProtocolTestServer Server;

    private static bool TestServerEnabled {
        get {
            var value = Environment.GetEnvironmentVariable("BROWS_PROTOCOL_TEST_SERVERS");
            return value is "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static async Task<List<FtpEntry>> List(FtpProviderBase provider, Uri uri, CancellationToken token) {
        var entries = new List<FtpEntry>();
        await foreach (var entry in provider.ListEntries(uri, token).WithCancellation(token)) {
            entries.Add(entry);
        }
        return entries;
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
    public async Task ServerAbsoluteRootProvider_ListsOpensAndMutatesServerRootEntries() {
        var rootUri = new UriBuilder("ftp", Server.Info.Host, Server.Info.FtpPort, "//").Uri;
        using var provider = new TestProvider(rootUri);
        SetProviderConfig(provider);
        var client = provider.ClientAt(rootUri);
        client.Resolve = false;
        client.Credential.Username = Server.Info.LoginHomeUsername;
        await client.Credential.ProtectPassword(Server.Info.Password, SecurityDataKind.PlainText, Token);

        var rootEntries = await List(provider, rootUri, Token);
        var fixtures = rootEntries.Single(entry => entry.Name == "fixtures");
        var fixtureEntries = await List(provider, fixtures.Uri, Token);
        var alpha = fixtureEntries.Single(entry => entry.Name == "alpha.txt");
        var alphaSource = alpha.Stream.Source;
        using var ready = await alphaSource.StreamReady(Token);
        using var stream = alphaSource.Stream;
        using var contents = new MemoryStream();
        await stream.CopyToAsync(contents, Token);

        var suffix = Guid.NewGuid().ToString("N");
        var deleteName = "provider-root-delete-" + suffix + ".txt";
        var renameName = "provider-root-rename-" + suffix + ".txt";
        var renamedName = "provider-root-renamed-" + suffix + ".txt";
        var createName = "provider-root-created-" + suffix;
        var serverRootFixtures = Path.Combine(Server.Info.RootPath, "fixtures");
        var loginHomeFixtures = Path.Combine(Server.Info.RootPath, "home", "fixtures");
        var loginHomeDeleteFile = Path.Combine(loginHomeFixtures, deleteName);
        File.WriteAllText(Path.Combine(serverRootFixtures, deleteName), "server-root delete\n");
        File.WriteAllText(Path.Combine(loginHomeFixtures, deleteName), "login-home delete\n");
        File.WriteAllText(Path.Combine(serverRootFixtures, renameName), "server-root rename\n");
        File.WriteAllText(Path.Combine(loginHomeFixtures, renameName), "login-home rename\n");

        fixtureEntries = await List(provider, fixtures.Uri, Token);
        var deleteEntry = fixtureEntries.Single(entry => entry.Name == deleteName);
        var renameEntry = fixtureEntries.Single(entry => entry.Name == renameName);
        await provider.DeleteRecursive(deleteEntry, static _ => { }, Token);
        var renameTarget = FileProtocolUri.CombinePath(fixtures.Uri, renamedName);
        await renameEntry.Client().Rename(renameEntry.RemotePath, renameTarget, Token);
        var createPath = FileProtocolUri.CombinePath(fixtures.Uri, createName);
        await fixtures.Client().CreateDirectory(createPath, Token);
        var loginHomeDeleteExists = File.Exists(loginHomeDeleteFile);

        Assert.Multiple(() => {
            Assert.That(fixtures.Uri.AbsolutePath, Is.EqualTo("//fixtures/"));
            Assert.That(alpha.Uri.AbsolutePath, Is.EqualTo("//fixtures/alpha.txt"));
            Assert.That(Encoding.UTF8.GetString(contents.ToArray()), Is.EqualTo("alpha fixture\n"));
            Assert.That(File.Exists(Path.Combine(serverRootFixtures, deleteName)), Is.False);
            Assert.That(loginHomeDeleteExists, Is.True);
            if (loginHomeDeleteExists) {
                Assert.That(File.ReadAllText(loginHomeDeleteFile), Is.EqualTo("login-home delete\n"));
            }
            Assert.That(File.Exists(Path.Combine(serverRootFixtures, renameName)), Is.False);
            Assert.That(
                File.ReadAllText(Path.Combine(serverRootFixtures, renamedName)),
                Is.EqualTo("server-root rename\n"));
            Assert.That(
                File.ReadAllText(Path.Combine(loginHomeFixtures, renameName)),
                Is.EqualTo("login-home rename\n"));
            Assert.That(File.Exists(Path.Combine(loginHomeFixtures, renamedName)), Is.False);
            Assert.That(Directory.Exists(Path.Combine(serverRootFixtures, createName)), Is.True);
            Assert.That(Directory.Exists(Path.Combine(loginHomeFixtures, createName)), Is.False);
        });
    }

    private sealed class TestProvider : FtpProviderBase {
        public TestProvider(Uri uri) : base(new TestFactory(), uri, null) {
        }

        private sealed class TestFactory : IFtpProviderFactory {
            public FtpClientCache Client { get; } = new();
        }
    }
}
