using Brows.Commands;
using Brows.Composition;
using Brows.Entries;
using Brows.Native;
using Brows.Panels;
using Brows.Providers;
using Brows.SFTP.Native;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
[Category("Integration")]
internal sealed class SftpLiveIntegrationTest {
    private static string ServerScriptPath() {
        return ProtocolTestServerInfrastructure.FindServerScript(
                   AppContext.BaseDirectory,
                   Environment.CurrentDirectory,
                   Environment.GetEnvironmentVariable("BROWS_PROTOCOL_TEST_SERVER_SCRIPT"),
                   File.Exists) ??
               throw new FileNotFoundException("The protocol-test server script was not found.");
    }

    private static string RepositoryRoot() {
        return ProtocolTestServerInfrastructure.FindRepositoryRoot(ServerScriptPath());
    }

    private static string Python(string repositoryRoot) {
        return ProtocolTestServerInfrastructure.FindPython(
            repositoryRoot,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetEnvironmentVariable("BROWS_PROTOCOL_TEST_PYTHON"),
            File.Exists);
    }

    private static SecureString Secure(string value) {
        var result = new SecureString();
        foreach (var character in value) {
            result.AppendChar(character);
        }
        result.MakeReadOnly();
        return result;
    }

    private static Uri CreateUri(string host, int port, string username) {
        return new UriBuilder("sftp", host, port) {
            UserName = username,
            Path = "/"
        }.Uri;
    }

    private static void ChangeKnownHostKey(string path) {
        var lines = File.ReadAllLines(path);
        var lineIndex = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'));
        Assert.That(lineIndex, Is.GreaterThanOrEqualTo(0));
        var fields = lines[lineIndex].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.That(fields.Length, Is.GreaterThanOrEqualTo(3));
        var key = fields[2].ToCharArray();
        Assert.That(key.Length, Is.GreaterThan(0));
        key[0] = key[0] == 'A' ? 'B' : 'A';
        fields[2] = new string(key);
        lines[lineIndex] = string.Join(' ', fields);
        File.WriteAllLines(path, lines);
    }

    private static T CreateProviderExport<T>(string name) where T : class {
        var type = typeof(SftpProvider).GetNestedType(name, BindingFlags.NonPublic);
        return Activator.CreateInstance(type, nonPublic: true) as T
            ?? throw new InvalidOperationException($"The SFTP provider export {name} could not be created.");
    }

    private static IImportAgent CreateProviderImport() {
        var import = new Mock<IImportAgent>();
        import.SetupGet(value => value.Ready).Returns(true);
        import.Setup(value => value.List<IEntryDataExport>(It.IsAny<bool>())).Returns([]);
        return import.Object;
    }

    private static void InitializeProvider(SftpProvider provider, IImportAgent import) {
        typeof(Provider)
            .GetProperty("Import", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(provider, import);
        typeof(EntryProvider)
            .GetProperty("Config", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(provider, new SftpConfig());
    }

    [Test]
    public async Task DisposableServer_ExercisesTrustListingReadWriteRenameAndDelete() {
        if (!string.Equals(Environment.GetEnvironmentVariable("BROWS_SFTP_INTEGRATION"), "1", StringComparison.Ordinal)) {
            Assert.Ignore("Set BROWS_SFTP_INTEGRATION=1 to run the disposable SFTP server integration test.");
        }

        var repositoryRoot = RepositoryRoot();
        var temporaryRoot = Path.Combine(Path.GetTempPath(), "brows-sftp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);
        var serverFile = ServerScriptPath();
        var closeFailureSignal = Path.Combine(temporaryRoot, "close-failure-signal");
        var connectStallSignal = Path.Combine(temporaryRoot, "connect-stall-signal");
        var connectReleaseFile = Path.Combine(temporaryRoot, "connect-release");
        var start = new ProcessStartInfo(Python(repositoryRoot)) {
            UseShellExecute = false,
            WorkingDirectory = repositoryRoot
        };
        start.ArgumentList.Add(serverFile);
        start.ArgumentList.Add("--ready-file");
        start.ArgumentList.Add(Path.Combine(temporaryRoot, "ready.json"));
        start.ArgumentList.Add("--root");
        start.ArgumentList.Add(temporaryRoot);
        start.ArgumentList.Add("--sftp-close-failure-prefix");
        start.ArgumentList.Add("/fixtures/close-failure");
        start.ArgumentList.Add("--sftp-close-failure-signal");
        start.ArgumentList.Add(closeFailureSignal);
        start.ArgumentList.Add("--sftp-connect-stall-number");
        start.ArgumentList.Add("2");
        start.ArgumentList.Add("--sftp-connect-stall-signal");
        start.ArgumentList.Add(connectStallSignal);
        start.ArgumentList.Add("--sftp-connect-release-file");
        start.ArgumentList.Add(connectReleaseFile);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("The SFTP test server could not start.");
        SftpClient client = null;
        SftpClientLease lease = null;
        SftpClientLease sharedLease = null;
        SftpClient canceledOwnerClient = null;
        SftpClient retryClient = null;
        SftpClient gateHeldClient = null;
        SftpClientLease gateHeldLease = null;
        GateHoldingReadStream gateHoldingReadStream = null;
        SftpNative native = null;
        try {
            var ready = await WaitForServer(process, Path.Combine(temporaryRoot, "ready.json"), CancellationToken.None);
            Assert.That(ready.KnownHostsPath, Is.Not.Null.And.Not.Empty);
            Assert.That(ready.RootPath, Is.Not.Null.And.Not.Empty);
            Assert.That(ready.Ipv6Host, Is.Not.Null.And.Not.Empty);
            Assert.That(ready.SftpIpv6Port, Is.GreaterThan(0));

            native = new SftpNative();
            Assert.That(await native.Loaded(CancellationToken.None), Is.True);
            var uri = CreateUri("127.0.0.1", ready.SftpPort, ready.Username);
            var clientKnownHosts = Path.Combine(temporaryRoot, "client-known_hosts");
            var factory = new SftpProviderFactory();
            lease = factory.Acquire(uri, clientKnownHosts);
            sharedLease = factory.Acquire(uri, clientKnownHosts);
            var provider = new SftpProvider(factory, uri, null, lease);
            var sharedProvider = new SftpProvider(factory, uri, null, sharedLease);
            var firstPane = new TestPanel { Provider = provider };
            var secondPane = new TestPanel { Provider = sharedProvider };
            client = lease.Client;
            Assert.That(sharedLease.Client, Is.SameAs(client));
            Assert.That(
                ((SftpProvider)firstPane.Provider).Client,
                Is.SameAs(((SftpProvider)secondPane.Provider).Client));

            var sharedClientGate = (SemaphoreSlim)typeof(SftpClient)
                .GetField("Gate", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(client);
            await sharedClientGate.WaitAsync();
            Task firstConnect;
            Task secondConnect;
            try {
                firstConnect = client.Connect(CancellationToken.None);
                secondConnect = sharedLease.Client.Connect(CancellationToken.None);
                using var canceledWaiter = new CancellationTokenSource();
                var canceledConnect = client.Connect(canceledWaiter.Token);
                canceledWaiter.Cancel();
                var canceledConnectError = await Assert.CatchAsync<OperationCanceledException>(
                    async () => await canceledConnect);
                Assert.That(canceledConnectError, Is.Not.Null);
            }
            finally {
                sharedClientGate.Release();
            }
            await Task.WhenAll(firstConnect, secondConnect).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.That(client.KnownHostState, Is.EqualTo(SftpKnownHostStatus.NotFound));
            Assert.That(client.Fingerprint, Is.Not.Null.And.Not.Empty);

            var retryKnownHosts = Path.Combine(temporaryRoot, "retry-known_hosts");
            File.WriteAllText(retryKnownHosts, string.Empty);
            var canceledOwnerKnownHosts = Path.Combine(temporaryRoot, "canceled-owner-known_hosts");
            canceledOwnerClient = new SftpClient(uri, canceledOwnerKnownHosts);
            using var canceledOwner = new CancellationTokenSource();
            var canceledOwnerConnect = canceledOwnerClient.Connect(canceledOwner.Token);
            await WaitForSignal(process, connectStallSignal);
            canceledOwner.Cancel();
            var canceledOwnerError = await Assert.CatchAsync<OperationCanceledException>(async () => {
                await canceledOwnerConnect.WaitAsync(TimeSpan.FromSeconds(5));
            });
            Assert.That(canceledOwnerError, Is.Not.Null);
            File.WriteAllText(connectReleaseFile, string.Empty);
            await canceledOwnerClient.Connect(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.That(canceledOwnerClient.KnownHostState, Is.EqualTo(SftpKnownHostStatus.NotFound));
            await canceledOwnerClient.DisposeAsync();
            canceledOwnerClient = null;

            retryClient = new SftpClient(uri, retryKnownHosts);
            using (File.Open(retryKnownHosts, FileMode.Open, FileAccess.Read, FileShare.None)) {
                var failedConnect = await Assert.CatchAsync<NativeErrorException>(
                    async () => await retryClient.Connect(CancellationToken.None));
                Assert.That(failedConnect, Is.Not.Null);
            }
            Assert.That(retryClient.KnownHostState, Is.Not.EqualTo(SftpKnownHostStatus.NotFound));
            File.Delete(retryKnownHosts);
            await retryClient.Connect(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.That(retryClient.KnownHostState, Is.EqualTo(SftpKnownHostStatus.NotFound));

            using var password = Secure(ready.Password);
            Assert.That(await client.AuthenticateByPassword(password, CancellationToken.None), Is.False);
            Assert.That(await client.TrustHostKey(CancellationToken.None), Is.True);
            Assert.That(client.KnownHostState, Is.EqualTo(SftpKnownHostStatus.Match));
            Assert.That(await client.AuthenticateByPassword(password, CancellationToken.None), Is.True);

            var mismatchKnownHosts = Path.Combine(temporaryRoot, "mismatch-known_hosts");
            File.Copy(clientKnownHosts, mismatchKnownHosts);
            ChangeKnownHostKey(mismatchKnownHosts);
            var mismatchClient = new SftpClient(uri, mismatchKnownHosts);
            try {
                await mismatchClient.Connect(CancellationToken.None);
                Assert.That(mismatchClient.KnownHostState, Is.EqualTo(SftpKnownHostStatus.Mismatch));
                Assert.That(await mismatchClient.AuthenticateByPassword(password, CancellationToken.None), Is.False);
                Assert.That(await mismatchClient.TrustHostKey(CancellationToken.None), Is.True);
                Assert.That(mismatchClient.KnownHostState, Is.EqualTo(SftpKnownHostStatus.Match));
                Assert.That(await mismatchClient.AuthenticateByPassword(password, CancellationToken.None), Is.True);
            }
            finally {
                await mismatchClient.DisposeAsync();
            }

            var wrongPasswordClient = new SftpClient(uri, clientKnownHosts);
            using var wrongPassword = Secure("incorrect-password");
            try {
                await wrongPasswordClient.Connect(CancellationToken.None);
                Assert.That(wrongPasswordClient.KnownHostState, Is.EqualTo(SftpKnownHostStatus.Match));
                Assert.That(await wrongPasswordClient.AuthenticateByPassword(wrongPassword, CancellationToken.None), Is.False);
            }
            finally {
                await wrongPasswordClient.DisposeAsync();
            }

            var entries = new List<SftpEntry>();
            var fixturesUri = new UriBuilder(uri) { Path = "/fixtures/" }.Uri;
            await foreach (var entry in provider.ListEntries(fixturesUri, CancellationToken.None)) {
                entries.Add(entry);
            }
            Assert.That(entries.Select(entry => entry.Name), Does.Contain("alpha.txt"));
            Assert.That(entries.Select(entry => entry.Name), Does.Contain("space name.txt"));
            Assert.That(entries.Select(entry => entry.Name), Does.Contain("nested"));
            if (ready.PosixOnlyFileAvailable) {
                Assert.That(entries.Select(entry => entry.Name), Does.Contain("colon:name.txt"));
            }
            Assert.That(entries.Any(entry => entry.Kind == FileProtocolEntryKind.Directory), Is.True);
            if (ready.SymlinksAvailable) {
                Assert.That(entries.Single(entry => entry.Name == "alpha-link.txt").Kind, Is.EqualTo(FileProtocolEntryKind.FileLink));
                Assert.That(entries.Single(entry => entry.Name == "nested-link").Kind, Is.EqualTo(FileProtocolEntryKind.DirectoryLink));
            }

            var recursiveTargetPath = "/fixtures/sftp-recursive-upload";
            await client.CreateDirectory(recursiveTargetPath, CancellationToken.None);
            await client.CreateDirectory("/fixtures/nested/source-empty", CancellationToken.None);
            var recursiveTargetUri = new UriBuilder(uri) { Path = recursiveTargetPath + "/" }.Uri;
            var recursiveTarget = new SftpProvider(factory, recursiveTargetUri, null, lease);
            var providerImport = CreateProviderImport();
            InitializeProvider(provider, providerImport);
            InitializeProvider(recursiveTarget, providerImport);
            var sourceNestedUri = new UriBuilder(uri) { Path = "/fixtures/nested/" }.Uri;
            var sourceNested = new SftpEntry(
                provider,
                new SftpMetadata("nested", 4, 0, 0, 0, 0x41ED, 0, 0),
                FileProtocolEntryKind.Directory,
                sourceNestedUri);
            var uploadIo = new List<IProvidedIO>();
            var provide = CreateProviderExport<IProvideIO>("ProvideIO");
            Assert.That(await provide.Work(uploadIo, new CommandSource([sourceNested]), provider, null, CancellationToken.None), Is.True);
            var uploadSources = new List<string>();
            await foreach (var source in uploadIo.Single().StreamSets.Single().StreamSource(CancellationToken.None)) {
                uploadSources.Add(source.RelativePath);
            }
            Assert.That(uploadSources, Does.Contain("fixtures/nested/child.txt"));
            Assert.That(uploadSources, Does.Contain("fixtures/nested/source-empty/"));
            var copy = CreateProviderExport<IWorkProvidedIO>("CopyProvidedIO");
            Assert.That(await copy.Work(uploadIo, recursiveTarget, null, CancellationToken.None), Is.True);
            var nestedDownloadPath = recursiveTargetPath + "/fixtures/nested";
            await client.CreateDirectory(nestedDownloadPath + "/download-empty", CancellationToken.None);
            var copiedEmptyDirectory = await client.Stat(
                nestedDownloadPath + "/source-empty",
                link: false,
                CancellationToken.None);
            Assert.That(SftpPosixMode.IsDirectory(copiedEmptyDirectory.Permissions), Is.True);

            var nestedUri = new UriBuilder(uri) { Path = nestedDownloadPath + "/" }.Uri;
            var nestedEntry = new SftpEntry(
                recursiveTarget,
                new SftpMetadata("nested", 4, 0, 0, 0, 0x41ED, 0, 0),
                FileProtocolEntryKind.Directory,
                nestedUri);
            var downloadIo = new List<IProvidedIO>();
            Assert.That(await provide.Work(downloadIo, new CommandSource([nestedEntry]), recursiveTarget, null, CancellationToken.None), Is.True);
            var downloadedRoot = Path.Combine(temporaryRoot, "recursive-download");
            var streamSets = downloadIo.SelectMany(item => item.StreamSets);
            foreach (var streamSet in streamSets) {
                await streamSet.Consume(
                    consuming: async (source, stream, _, token) => {
                        var relative = source.RelativePath.Replace('/', Path.DirectorySeparatorChar);
                        var localPath = Path.Combine(downloadedRoot, relative);
                        if (stream is null) {
                            Directory.CreateDirectory(localPath);
                            return;
                        }
                        Directory.CreateDirectory(Path.GetDirectoryName(localPath));
                        await using var destination = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
                        await stream.CopyToAsync(destination, token);
                    },
                    progress: null,
                    token: CancellationToken.None);
            }
            var downloadedChild = Path.Combine(downloadedRoot, "fixtures", "nested", "child.txt");
            Assert.That(await File.ReadAllTextAsync(downloadedChild), Is.EqualTo("nested fixture\n"));
            Assert.That(Directory.Exists(Path.Combine(downloadedRoot, "fixtures", "nested", "download-empty")), Is.True);

            var copiedRootUri = new UriBuilder(uri) { Path = nestedDownloadPath + "/" }.Uri;
            var copiedRoot = new SftpEntry(
                recursiveTarget,
                new SftpMetadata("nested", 4, 0, 0, 0, 0x41ED, 0, 0),
                FileProtocolEntryKind.Directory,
                copiedRootUri);
            await recursiveTarget.DeleteRecursive(copiedRoot, CancellationToken.None);
            await client.DeleteDirectory(recursiveTargetPath + "/fixtures", CancellationToken.None);
            await client.DeleteDirectory(recursiveTargetPath, CancellationToken.None);

            await using (var remote = await client.OpenRead("/fixtures/alpha.txt", CancellationToken.None))
            using (var content = new MemoryStream()) {
                await remote.CopyToAsync(content, CancellationToken.None);
                var expected = await File.ReadAllTextAsync(Path.Combine(ready.RootPath, "fixtures", "alpha.txt"));
                Assert.That(Encoding.UTF8.GetString(content.ToArray()), Is.EqualTo(expected));
            }

            await client.CreateDirectory("/fixtures/sftp-created", CancellationToken.None);
            const string createdFilePath = "/fixtures/sftp-created/new file.txt";
            await client.CreateFile(createdFilePath, CancellationToken.None);
            var createdFileMetadata = await client.Stat(createdFilePath, link: false, CancellationToken.None);
            Assert.That(createdFileMetadata.Size, Is.Zero);
            var preservedContent = Encoding.UTF8.GetBytes("keep existing file contents");
            await using (var createdFile = await client.OpenWrite(createdFilePath, CancellationToken.None)) {
                await createdFile.WriteAsync(preservedContent, CancellationToken.None);
            }
            await Assert.CatchAsync<NativeErrorException>(async () => {
                await client.CreateFile(createdFilePath, CancellationToken.None);
            });
            await using (var createdFile = await client.OpenRead(createdFilePath, CancellationToken.None))
            using (var content = new MemoryStream()) {
                await createdFile.CopyToAsync(content, CancellationToken.None);
                Assert.That(content.ToArray(), Is.EqualTo(preservedContent));
            }
            await client.DeleteFile(createdFilePath, CancellationToken.None);
            var upload = Encoding.UTF8.GetBytes("SFTP upload test");
            await using (var remote = await client.OpenWrite("/fixtures/sftp-created/space name.txt", CancellationToken.None)) {
                await remote.WriteAsync(upload, CancellationToken.None);
            }
            await client.Rename(
                "/fixtures/sftp-created/space name.txt",
                "/fixtures/sftp-created/renamed.txt",
                CancellationToken.None);
            await using (var remote = await client.OpenRead("/fixtures/sftp-created/renamed.txt", CancellationToken.None))
            using (var content = new MemoryStream()) {
                await remote.CopyToAsync(content, CancellationToken.None);
                Assert.That(content.ToArray(), Is.EqualTo(upload));
            }
            await client.DeleteFile("/fixtures/sftp-created/renamed.txt", CancellationToken.None);
            await client.DeleteDirectory("/fixtures/sftp-created", CancellationToken.None);

            const string closeFailureDirectory = "/fixtures/close-failure";
            await client.CreateDirectory(closeFailureDirectory, CancellationToken.None);
            var syncCloseFailure = await client.OpenWrite(closeFailureDirectory + "/sync.txt", CancellationToken.None);
            await syncCloseFailure.WriteAsync(Encoding.UTF8.GetBytes("sync close failure"), CancellationToken.None);
            Exception syncCloseError = null;
            try {
                syncCloseFailure.Dispose();
            }
            catch (Exception error) {
                syncCloseError = error;
            }

            var asyncCloseFailure = await client.OpenWrite(closeFailureDirectory + "/async.txt", CancellationToken.None);
            await asyncCloseFailure.WriteAsync(Encoding.UTF8.GetBytes("async close failure"), CancellationToken.None);
            Exception asyncCloseError = null;
            try {
                await asyncCloseFailure.DisposeAsync();
            }
            catch (Exception error) {
                asyncCloseError = error;
            }

            var closeFailureMoveSource = Path.Combine(temporaryRoot, "close-failure-move.txt");
            await File.WriteAllTextAsync(closeFailureMoveSource, "retain source when close fails");
            var closeFailureTargetUri = new UriBuilder(uri) { Path = closeFailureDirectory + "/" }.Uri;
            var closeFailureTarget = new SftpProvider(factory, closeFailureTargetUri, null, lease);
            InitializeProvider(closeFailureTarget, providerImport);
            var closeFailureMove = CreateProviderExport<IWorkProvidedIO>("MoveProvidedIO");
            var closeFailureProvided = new ProvidedIO(EntryStreamSet.FromFiles([closeFailureMoveSource]));
            Exception moveCloseError = null;
            try {
                await closeFailureMove.Work([closeFailureProvided], closeFailureTarget, null, CancellationToken.None);
            }
            catch (Exception error) {
                moveCloseError = error;
            }
            var moveSourceExists = File.Exists(closeFailureMoveSource);
            await client.DeleteFile(closeFailureDirectory + "/sync.txt", CancellationToken.None);
            await client.DeleteFile(closeFailureDirectory + "/async.txt", CancellationToken.None);
            await client.DeleteFile(closeFailureDirectory + "/close-failure-move.txt", CancellationToken.None);
            await client.DeleteDirectory(closeFailureDirectory, CancellationToken.None);

            const string partialPath = "/fixtures/cancelled-upload.txt";
            await using (var remote = await client.OpenWrite(partialPath, CancellationToken.None)) {
                await remote.WriteAsync(Encoding.UTF8.GetBytes("partial"), CancellationToken.None);
                using var canceled = new CancellationTokenSource();
                canceled.Cancel();
                await Assert.CatchAsync<OperationCanceledException>(async () => {
                    await remote.WriteAsync(Encoding.UTF8.GetBytes("must not be appended"), canceled.Token);
                });
            }
            var partialMetadata = await client.Stat(partialPath, link: false, CancellationToken.None);
            Assert.That(partialMetadata.Size, Is.EqualTo((ulong)"partial".Length));
            await client.DeleteFile(partialPath, CancellationToken.None);

            var ipv6Uri = CreateUri(ready.Ipv6Host, ready.SftpIpv6Port, ready.Username);
            var ipv6KnownHosts = Path.Combine(temporaryRoot, "ipv6-known_hosts");
            var ipv6Client = new SftpClient(ipv6Uri, ipv6KnownHosts);
            try {
                await ipv6Client.Connect(CancellationToken.None);
                Assert.That(ipv6Client.HostName, Is.EqualTo(ready.Ipv6Host.Trim('[', ']')));
                Assert.That(ipv6Client.KnownHostState, Is.EqualTo(SftpKnownHostStatus.NotFound));
                Assert.That(await ipv6Client.TrustHostKey(CancellationToken.None), Is.True);
                Assert.That(await ipv6Client.AuthenticateByPassword(password, CancellationToken.None), Is.True);
                using var directory = await ipv6Client.OpenDirectory("/fixtures", CancellationToken.None);
                Assert.That(await ipv6Client.ReadDirectory(directory, CancellationToken.None), Is.Not.Null);
            }
            finally {
                await ipv6Client.DisposeAsync();
            }

            var sourcePath = Path.Combine(temporaryRoot, "move-source.txt");
            await File.WriteAllTextAsync(sourcePath, "keep source when destination fails");
            var failedDestinationUri = new UriBuilder(uri) { Path = "/fixtures/alpha.txt/" }.Uri;
            var failedDestination = new SftpProvider(factory, failedDestinationUri, null, lease);
            var moveType = typeof(SftpProvider).GetNestedType("MoveProvidedIO", BindingFlags.NonPublic);
            var move = (IWorkProvidedIO)Activator.CreateInstance(moveType, nonPublic: true);
            var provided = new ProvidedIO(EntryStreamSet.FromFiles([sourcePath]));
            var failedDestinationError = await Assert.CatchAsync<NativeErrorException>(async () => {
                await move.Work([provided], failedDestination, null, CancellationToken.None);
            });
            Assert.That(failedDestinationError, Is.Not.Null);
            Assert.That(File.Exists(sourcePath), Is.True);

            var clientDisposalSourcePath = Path.Combine(temporaryRoot, "client-disposal-move.txt");
            await File.WriteAllTextAsync(clientDisposalSourcePath, "retain source when client closes before finalization");
            const string clientDisposalTargetPath = "/fixtures/client-disposal";
            await client.CreateDirectory(clientDisposalTargetPath, CancellationToken.None);
            var clientDisposalTargetUri = new UriBuilder(uri) { Path = clientDisposalTargetPath + "/" }.Uri;
            var clientDisposalTarget = new SftpProvider(factory, clientDisposalTargetUri, null, lease);
            InitializeProvider(clientDisposalTarget, providerImport);
            Exception clientDisposalMoveError = null;
            try {
                var streamSet = new ClientDisposalStreamSet(clientDisposalSourcePath, client);
                await move.Work([new ProvidedIO(streamSet)], clientDisposalTarget, null, CancellationToken.None);
            }
            catch (Exception error) {
                clientDisposalMoveError = error;
            }
            var clientDisposalErrorObject = clientDisposalMoveError is ObjectDisposedException disposedError
                ? disposedError.ObjectName
                : null;
            var clientDisposalSourceExists = File.Exists(clientDisposalSourcePath);
            Assert.Multiple(() => {
                Assert.That(clientDisposalMoveError, Is.TypeOf<ObjectDisposedException>());
                Assert.That(clientDisposalErrorObject, Is.EqualTo(typeof(SftpFile).FullName));
                Assert.That(clientDisposalSourceExists, Is.True);
                Assert.That(syncCloseError, Is.TypeOf<NativeErrorException>());
                Assert.That(asyncCloseError, Is.TypeOf<NativeErrorException>());
                Assert.That(moveCloseError, Is.TypeOf<NativeErrorException>());
                Assert.That(moveSourceExists, Is.True);
            });

            var gateHeldSourcePath = Path.Combine(temporaryRoot, "gate-held-close-move");
            Directory.CreateDirectory(gateHeldSourcePath);
            gateHeldLease = factory.Acquire(uri, clientKnownHosts);
            gateHeldClient = gateHeldLease.Client;
            await ConnectAndAuthenticate(gateHeldClient, password);
            await gateHeldClient.CreateDirectory(closeFailureDirectory, CancellationToken.None);
            var gateHeldTarget = new SftpProvider(factory, closeFailureTargetUri, null, gateHeldLease);
            InitializeProvider(gateHeldTarget, providerImport);
            var clientGate = (SemaphoreSlim)typeof(SftpClient)
                .GetField("Gate", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(gateHeldClient);
            var clientShutdown = (CancellationTokenSource)typeof(SftpClient)
                .GetField("Shutdown", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(gateHeldClient);
            gateHoldingReadStream = new GateHoldingReadStream(
                Encoding.UTF8.GetBytes("hold the SFTP gate until cleanup starts"),
                clientGate);
            var fallbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var shutdownRegistration = clientShutdown.Token.Register(() => {
                fallbackStarted.TrySetResult();
                gateHoldingReadStream.ReleaseGate();
            });
            File.Delete(closeFailureSignal);
            var gateHeldStopwatch = Stopwatch.StartNew();
            var gateHeldMove = closeFailureMove.Work(
                [new ProvidedIO(new GateHoldingStreamSet(gateHeldSourcePath, gateHoldingReadStream))],
                gateHeldTarget,
                null,
                CancellationToken.None);
            var gateOrMove = await Task.WhenAny(
                gateHoldingReadStream.GateAcquired,
                gateHeldMove).WaitAsync(TimeSpan.FromSeconds(5));
            if (gateOrMove == gateHeldMove) {
                await gateHeldMove;
                throw new InvalidOperationException("The move completed before the SFTP gate was held.");
            }
            await gateHoldingReadStream.GateAcquired.WaitAsync(TimeSpan.FromSeconds(5));
            await fallbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Exception gateHeldMoveError = null;
            try {
                await gateHeldMove.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception error) {
                gateHeldMoveError = error;
            }
            gateHeldStopwatch.Stop();
            var gateHeldErrorObject = gateHeldMoveError is ObjectDisposedException gateHeldDisposedError
                ? gateHeldDisposedError.ObjectName
                : null;
            Assert.Multiple(() => {
                Assert.That(gateHeldMoveError, Is.TypeOf<ObjectDisposedException>());
                Assert.That(gateHeldErrorObject, Is.EqualTo(typeof(SftpFile).FullName));
                Assert.That(Directory.Exists(gateHeldSourcePath), Is.True);
                Assert.That(File.Exists(closeFailureSignal), Is.True);
                Assert.That(gateHeldMove.IsCompleted, Is.True);
                Assert.That(gateHeldStopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
            });
        }
        finally {
            gateHoldingReadStream?.ReleaseGate();
            gateHeldLease?.Dispose();
            if (gateHeldClient is not null) {
                await gateHeldClient.DisposeAsync();
            }
            if (retryClient is not null) {
                await retryClient.DisposeAsync();
            }
            if (canceledOwnerClient is not null) {
                await canceledOwnerClient.DisposeAsync();
            }
            sharedLease?.Dispose();
            lease?.Dispose();
            if (client is not null) {
                await client.DisposeAsync();
            }
            if (native is not null) {
                await native.Freed(CancellationToken.None);
            }
            if (!process.HasExited) {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    [Test]
    [Category("Integration")]
    public async Task DisposableServer_BoundsStalledAndDroppedSftpCleanup() {
        var integrationEnabled = string.Equals(
            Environment.GetEnvironmentVariable("BROWS_SFTP_INTEGRATION"),
            "1",
            StringComparison.Ordinal);
        if (!integrationEnabled) {
            Assert.Ignore("Set BROWS_SFTP_INTEGRATION=1 to run the disposable SFTP server integration test.");
        }

        var repositoryRoot = RepositoryRoot();
        var temporaryRoot = Path.Combine(Path.GetTempPath(), "brows-sftp-cleanup-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);
        var serverFile = ServerScriptPath();
        var readyFile = Path.Combine(temporaryRoot, "ready.json");
        var stopFile = Path.Combine(temporaryRoot, "stop-server");
        var stallSignal = Path.Combine(temporaryRoot, "close-stalled");
        var statSignal = Path.Combine(temporaryRoot, "stat-stalled");
        var dropSignal = Path.Combine(temporaryRoot, "close-dropped");
        var start = new ProcessStartInfo(Python(repositoryRoot)) {
            UseShellExecute = false,
            WorkingDirectory = repositoryRoot
        };
        start.ArgumentList.Add(serverFile);
        start.ArgumentList.Add("--ready-file");
        start.ArgumentList.Add(readyFile);
        start.ArgumentList.Add("--root");
        start.ArgumentList.Add(temporaryRoot);
        start.ArgumentList.Add("--sftp-close-stall-prefix");
        start.ArgumentList.Add("/fixtures/close-stall");
        start.ArgumentList.Add("--sftp-close-stall-signal");
        start.ArgumentList.Add(stallSignal);
        start.ArgumentList.Add("--sftp-stat-stall-prefix");
        start.ArgumentList.Add("/fixtures/stat-stall");
        start.ArgumentList.Add("--sftp-stat-stall-signal");
        start.ArgumentList.Add(statSignal);
        start.ArgumentList.Add("--sftp-close-drop-prefix");
        start.ArgumentList.Add("/fixtures/close-drop");
        start.ArgumentList.Add("--sftp-close-drop-signal");
        start.ArgumentList.Add(dropSignal);
        start.ArgumentList.Add("--stop-file");
        start.ArgumentList.Add(stopFile);

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("The SFTP test server could not start.");
        var clients = new List<SftpClient>();
        SftpNative native = null;
        SftpClientLease lease = null;
        SftpClientLease sharedLease = null;
        SftpClientLease replacementLease = null;
        SftpClient deferredCleanupClient = null;
        SemaphoreSlim deferredCleanupGate = null;
        Task deferredDisposeTask = null;
        Task deferredCleanupTask = null;
        var deferredCleanupGateHeld = false;
        var deferredCleanupDisposeStarted = false;
        try {
            var ready = await WaitForServer(process, readyFile, CancellationToken.None);
            native = new SftpNative();
            Assert.That(await native.Loaded(CancellationToken.None), Is.True);
            var uri = CreateUri("localhost", ready.SftpPort, ready.Username);
            using var password = Secure(ready.Password);

            var factory = new SftpProviderFactory();
            var knownHostsFile = Path.Combine(temporaryRoot, "shared-known_hosts");
            lease = factory.Acquire(uri, knownHostsFile);
            sharedLease = factory.Acquire(uri, knownHostsFile);
            var client = lease.Client;
            clients.Add(client);
            Assert.That(sharedLease.Client, Is.SameAs(client));
            await ConnectAndAuthenticate(client, password);
            await client.CreateDirectory("/fixtures/close-stall", CancellationToken.None);

            var asyncStream = await client.OpenWrite("/fixtures/close-stall/async.txt", CancellationToken.None);
            var syncStream = await client.OpenWrite("/fixtures/close-stall/sync.txt", CancellationToken.None);
            await asyncStream.WriteAsync(Encoding.UTF8.GetBytes("async close"), CancellationToken.None);
            await syncStream.WriteAsync(Encoding.UTF8.GetBytes("sync close"), CancellationToken.None);
            var asyncClose = asyncStream.DisposeAsync().AsTask();
            await WaitForSignal(process, stallSignal);

            using var canceled = new CancellationTokenSource();
            var canceledWaiter = client.Stat("/fixtures/alpha.txt", link: false, canceled.Token);
            canceled.Cancel();
            var canceledError = await Assert.CatchAsync<OperationCanceledException>(async () => await canceledWaiter);
            var queuedWaiter = client.Stat("/fixtures/alpha.txt", link: false, CancellationToken.None);
            var syncClose = Task.Run(() => {
                SynchronizationContext.SetSynchronizationContext(new NonPumpingSynchronizationContext());
                try {
                    syncStream.Dispose();
                }
                finally {
                    SynchronizationContext.SetSynchronizationContext(null);
                }
            });

            var asyncCloseError = await Assert.CatchAsync<NativeErrorException>(async () => {
                await asyncClose.WaitAsync(TimeSpan.FromSeconds(5));
            });
            Exception syncCloseError = null;
            try {
                await syncClose.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex) {
                syncCloseError = ex;
            }
            Exception queuedError = null;
            try {
                await queuedWaiter.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex) {
                queuedError = ex;
            }
            Assert.Multiple(() => {
                Assert.That(canceledError, Is.Not.Null);
                Assert.That(asyncCloseError, Is.Not.Null);
                Assert.That(asyncCloseError.Error, Is.EqualTo(2111086));
                Assert.That(
                    syncCloseError,
                    Is.Null.Or.InstanceOf<NativeErrorException>().Or.InstanceOf<ObjectDisposedException>());
                Assert.That(queuedError, Is.Null.Or.InstanceOf<IOException>().Or.InstanceOf<ObjectDisposedException>());
                Assert.That(asyncClose.IsCompleted, Is.True);
                Assert.That(syncClose.IsCompleted, Is.True);
                Assert.That(queuedWaiter.IsCompleted, Is.True);
            });
            if (syncCloseError is NativeErrorException syncNativeError) {
                Assert.That(syncNativeError.Error, Is.EqualTo(2111086));
            }

            sharedLease.Dispose();
            lease.Dispose();
            replacementLease = factory.Acquire(uri, knownHostsFile);
            var replacementClient = replacementLease.Client;
            clients.Add(replacementClient);
            Assert.That(replacementClient, Is.Not.SameAs(client));
            await ConnectAndAuthenticate(replacementClient, password);
            var operationStream = await replacementClient.OpenWrite(
                "/fixtures/close-stall/operation-holds-gate.txt",
                CancellationToken.None);
            var disposedStream = await replacementClient.OpenWrite(
                "/fixtures/close-stall/disposed-while-stat.txt",
                CancellationToken.None);
            var stalledStat = replacementClient.Stat("/fixtures/stat-stall/entry", link: false, CancellationToken.None);
            await WaitForSignal(process, statSignal);
            var shutdownStopwatch = Stopwatch.StartNew();
            var closeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var blockedDispose = Task.Run(() => {
                SynchronizationContext.SetSynchronizationContext(new NonPumpingSynchronizationContext());
                try {
                    closeStarted.TrySetResult();
                    try {
                        disposedStream.Dispose();
                    }
                    catch (ObjectDisposedException) {
                    }
                }
                finally {
                    SynchronizationContext.SetSynchronizationContext(null);
                }
            });
            await closeStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
            var firstDispose = replacementClient.DisposeAsync().AsTask();
            var repeatedDispose = replacementClient.DisposeAsync().AsTask();
            await Task.WhenAll(blockedDispose, firstDispose, repeatedDispose).WaitAsync(TimeSpan.FromSeconds(5));
            var stalledStatError = await Assert.CatchAsync<ObjectDisposedException>(async () => {
                await stalledStat.WaitAsync(TimeSpan.FromSeconds(5));
            });
            var remainingStreamError = await Assert.CatchAsync<ObjectDisposedException>(async () => {
                await operationStream.DisposeAsync();
            });
            Assert.Multiple(() => {
                Assert.That(blockedDispose.IsCompleted, Is.True);
                Assert.That(shutdownStopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
                Assert.That(stalledStatError, Is.Not.Null);
                Assert.That(remainingStreamError, Is.Not.Null);
            });

            File.Delete(statSignal);
            var recoveredLease = factory.Acquire(uri, knownHostsFile);
            var recoveredClient = recoveredLease.Client;
            clients.Add(recoveredClient);
            Assert.That(recoveredClient, Is.Not.SameAs(replacementClient));
            await ConnectAndAuthenticate(recoveredClient, password);
            recoveredLease.Dispose();

            File.Delete(stallSignal);
            var directoryClient = new SftpClient(uri, Path.Combine(temporaryRoot, "directory-known_hosts"));
            clients.Add(directoryClient);
            await ConnectAndAuthenticate(directoryClient, password);
            var directory = await directoryClient.OpenDirectory("/fixtures/close-stall", CancellationToken.None);
            var directoryClose = directory.DisposeAsync().AsTask();
            await WaitForSignal(process, stallSignal);
            var directoryCloseError = await Assert.CatchAsync<NativeErrorException>(async () => {
                await directoryClose.WaitAsync(TimeSpan.FromSeconds(5));
            });
            Assert.That(directoryCloseError.Error, Is.EqualTo(2111086));

            File.Delete(stallSignal);
            var disposalClient = new SftpClient(uri, Path.Combine(temporaryRoot, "disposal-known_hosts"));
            clients.Add(disposalClient);
            await ConnectAndAuthenticate(disposalClient, password);
            var disposalStream = await disposalClient.OpenWrite(
                "/fixtures/close-stall/disposal.txt",
                CancellationToken.None);
            var disposeClient = disposalClient.DisposeAsync().AsTask();
            await WaitForSignal(process, stallSignal);
            await disposeClient.WaitAsync(TimeSpan.FromSeconds(5));
            var disposedClientError = await Assert.CatchAsync<ObjectDisposedException>(async () => {
                await disposalStream.DisposeAsync();
            });
            Assert.That(disposedClientError.ObjectName, Is.EqualTo(typeof(SftpFile).FullName));

            var dropClient = new SftpClient(uri, Path.Combine(temporaryRoot, "drop-known_hosts"));
            clients.Add(dropClient);
            await ConnectAndAuthenticate(dropClient, password);
            await dropClient.CreateDirectory("/fixtures/close-drop", CancellationToken.None);
            var dropStream = await dropClient.OpenWrite("/fixtures/close-drop/file.txt", CancellationToken.None);
            var dropClose = dropStream.DisposeAsync().AsTask();
            await WaitForSignal(process, dropSignal);
            var dropCloseError = await Assert.CatchAsync<NativeErrorException>(async () => {
                await dropClose.WaitAsync(TimeSpan.FromSeconds(5));
            });
            Assert.That(dropCloseError.Error, Is.EqualTo(2111086));
            await dropClient.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

            deferredCleanupClient = new SftpClient(uri, Path.Combine(temporaryRoot, "deferred-cleanup-known_hosts"));
            await ConnectAndAuthenticate(deferredCleanupClient, password);
            await deferredCleanupClient.Stat("/fixtures/alpha.txt", link: false, CancellationToken.None);
            deferredCleanupGate = (SemaphoreSlim)typeof(SftpClient)
                .GetField("Gate", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(deferredCleanupClient);
            deferredCleanupGate.Wait();
            deferredCleanupGateHeld = true;
            var deferredDisposeStopwatch = Stopwatch.StartNew();
            deferredDisposeTask = deferredCleanupClient.DisposeAsync().AsTask();
            deferredCleanupDisposeStarted = true;
            var deferredDisposeError = await Assert.CatchAsync<TimeoutException>(async () => {
                await deferredDisposeTask.WaitAsync(TimeSpan.FromSeconds(5));
            });
            deferredDisposeStopwatch.Stop();
            deferredCleanupGate.Release();
            deferredCleanupGateHeld = false;
            deferredCleanupTask = (Task)typeof(SftpClient)
                .GetProperty("DeferredCleanup", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(deferredCleanupClient);
            await deferredCleanupTask.WaitAsync(TimeSpan.FromSeconds(5));
            var nativeHandle = typeof(NativeType).GetField("_Handle", BindingFlags.Instance | BindingFlags.NonPublic);
            var deferredConnection = typeof(SftpClient)
                .GetField("Connection", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(deferredCleanupClient);
            var deferredSftp = typeof(SftpClient)
                .GetProperty("Sftp", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(deferredCleanupClient);
            Assert.Multiple(() => {
                Assert.That(deferredDisposeError, Is.TypeOf<TimeoutException>());
                Assert.That(deferredDisposeStopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
                Assert.That(deferredCleanupTask.IsCompleted, Is.True);
                Assert.That((IntPtr)nativeHandle.GetValue(deferredConnection), Is.EqualTo(IntPtr.Zero));
                Assert.That((IntPtr)nativeHandle.GetValue(deferredSftp), Is.EqualTo(IntPtr.Zero));
            });
        }
        finally {
            if (deferredCleanupGateHeld) {
                deferredCleanupGate.Release();
            }
            if (deferredDisposeTask is not null && !deferredDisposeTask.IsCompleted) {
                try {
                    await deferredDisposeTask.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (TimeoutException) {
                }
            }
            if (deferredCleanupTask is null && deferredCleanupDisposeStarted && deferredCleanupClient is not null) {
                deferredCleanupTask = (Task)typeof(SftpClient)
                    .GetProperty("DeferredCleanup", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(deferredCleanupClient);
            }
            if (deferredCleanupTask is not null) {
                await deferredCleanupTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            else if (deferredCleanupClient is not null && !deferredCleanupDisposeStarted) {
                await deferredCleanupClient.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            }
            File.WriteAllText(stopFile, "stop\n");
            if (!process.HasExited) {
                try {
                    await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (TimeoutException) {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None);
                }
            }
            foreach (var client in clients.Distinct()) {
                await client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            }
            sharedLease?.Dispose();
            lease?.Dispose();
            replacementLease?.Dispose();
            if (native is not null) {
                await native.Freed(CancellationToken.None);
            }
            if (Directory.Exists(temporaryRoot)) {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    [Test]
    [Category("Integration")]
    public async Task DisposableServer_SynchronousWriteDoesNotCaptureNonPumpingContext() {
        if (!string.Equals(
            Environment.GetEnvironmentVariable("BROWS_SFTP_INTEGRATION"),
            "1",
            StringComparison.Ordinal)) {
            Assert.Ignore("Set BROWS_SFTP_INTEGRATION=1 to run the disposable SFTP server integration test.");
        }

        var repositoryRoot = RepositoryRoot();
        var temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "brows-sftp-write-context-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);
        var readyFile = Path.Combine(temporaryRoot, "ready.json");
        var stopFile = Path.Combine(temporaryRoot, "stop-server");
        var start = new ProcessStartInfo(Python(repositoryRoot)) {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = repositoryRoot
        };
        start.ArgumentList.Add(ServerScriptPath());
        start.ArgumentList.Add("--ready-file");
        start.ArgumentList.Add(readyFile);
        start.ArgumentList.Add("--root");
        start.ArgumentList.Add(temporaryRoot);
        start.ArgumentList.Add("--stop-file");
        start.ArgumentList.Add(stopFile);

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("The SFTP test server could not start.");
        var clients = new List<SftpClient>();
        SftpNative native = null;
        SftpFileStream writeStream = null;
        SemaphoreSlim clientGate = null;
        var clientGateHeld = false;
        Task writeTask = null;
        RecordingNonPumpingSynchronizationContext writeContext = null;
        try {
            var ready = await WaitForServer(process, readyFile, CancellationToken.None);
            native = new SftpNative();
            Assert.That(await native.Loaded(CancellationToken.None), Is.True);
            var uri = CreateUri("localhost", ready.SftpPort, ready.Username);
            using var password = Secure(ready.Password);
            var client = new SftpClient(uri, Path.Combine(temporaryRoot, "known_hosts"));
            clients.Add(client);
            await ConnectAndAuthenticate(client, password);
            await client.CreateDirectory("/fixtures/non-pumping-write", CancellationToken.None);
            const string remotePath = "/fixtures/non-pumping-write/payload.txt";
            writeStream = (SftpFileStream)await client.OpenWrite(remotePath, CancellationToken.None);

            clientGate = (SemaphoreSlim)typeof(SftpClient)
                .GetField("Gate", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(client);
            await clientGate.WaitAsync();
            clientGateHeld = true;
            var asyncWaiters = typeof(SemaphoreSlim).GetField(
                "m_asyncHead",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("SemaphoreSlim's asynchronous waiter field was not found.");
            var payload = Encoding.UTF8.GetBytes("write continuation must not capture the UI context");
            var writeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            writeContext = new RecordingNonPumpingSynchronizationContext();
            writeTask = Task.Factory.StartNew(
                () => {
                    SynchronizationContext.SetSynchronizationContext(writeContext);
                    try {
                        writeStarted.TrySetResult();
                        writeStream.Write(payload, 0, payload.Length);
                    }
                    finally {
                        SynchronizationContext.SetSynchronizationContext(null);
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            await writeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var writeWaiterWasQueued = await Task.Run(() => SpinWait.SpinUntil(
                () => asyncWaiters.GetValue(clientGate) is not null,
                TimeSpan.FromSeconds(5)));
            Assert.That(writeWaiterWasQueued, Is.True, "The write must be awaiting the held SFTP operation gate.");

            clientGate.Release();
            clientGateHeld = false;
            var finishedOrCapturedContext = await Task.WhenAny(writeTask, writeContext.CallbackPosted)
                .WaitAsync(TimeSpan.FromSeconds(5));
            var capturedContext = finishedOrCapturedContext == writeContext.CallbackPosted;
            if (capturedContext) {
                writeContext.RunPendingCallbacks();
            }
            await writeTask.WaitAsync(TimeSpan.FromSeconds(5));
            await writeStream.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            writeStream = null;

            await using var remote = await client.OpenRead(remotePath, CancellationToken.None);
            using var contents = new MemoryStream();
            await remote.CopyToAsync(contents, CancellationToken.None);
            Assert.Multiple(() => {
                Assert.That(
                    capturedContext,
                    Is.False,
                    "A synchronous write must not wait for a captured context callback.");
                Assert.That(contents.ToArray(), Is.EqualTo(payload));
            });
        }
        finally {
            if (clientGateHeld) {
                clientGate.Release();
            }
            if (writeTask is not null && !writeTask.IsCompleted) {
                try {
                    var completedOrPosted = await Task.WhenAny(writeTask, writeContext.CallbackPosted)
                        .WaitAsync(TimeSpan.FromSeconds(5));
                    if (completedOrPosted == writeContext.CallbackPosted) {
                        writeContext.RunPendingCallbacks();
                    }
                    await writeTask.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch {
                }
            }
            if (writeStream is not null) {
                try {
                    await writeStream.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch {
                }
            }
            File.WriteAllText(stopFile, "stop\n");
            if (!process.HasExited) {
                try {
                    await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (TimeoutException) {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None);
                }
            }
            foreach (var client in clients) {
                await client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            }
            if (Directory.Exists(temporaryRoot)) {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    private static async Task ConnectAndAuthenticate(SftpClient client, SecureString password) {
        await client.Connect(CancellationToken.None);
        Assert.That(await client.TrustHostKey(CancellationToken.None), Is.True);
        Assert.That(await client.AuthenticateByPassword(password, CancellationToken.None), Is.True);
    }

    private static async Task WaitForSignal(Process process, string signalFile) {
        var stopwatch = Stopwatch.StartNew();
        while (!File.Exists(signalFile)) {
            if (process.HasExited) {
                throw new InvalidOperationException($"The SFTP test server exited with code {process.ExitCode}.");
            }
            if (stopwatch.Elapsed > TimeSpan.FromSeconds(15)) {
                throw new TimeoutException($"The SFTP server did not create the signal file {signalFile}.");
            }
            await Task.Delay(TimeSpan.FromMilliseconds(25));
        }
    }

    private sealed class RecordingNonPumpingSynchronizationContext : SynchronizationContext {
        private readonly Lock Locker = new();
        private readonly Queue<(SendOrPostCallback Callback, object State)> Callbacks = new();
        private readonly TaskCompletionSource CallbackPostedCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task CallbackPosted => CallbackPostedCompletion.Task;

        internal void RunPendingCallbacks() {
            for (;;) {
                (SendOrPostCallback Callback, object State) callback;
                lock (Locker) {
                    if (Callbacks.Count == 0) {
                        return;
                    }
                    callback = Callbacks.Dequeue();
                }
                var previous = Current;
                SetSynchronizationContext(this);
                try {
                    callback.Callback(callback.State);
                }
                finally {
                    SetSynchronizationContext(previous);
                }
            }
        }

        public override void Post(SendOrPostCallback callback, object state) {
            lock (Locker) {
                Callbacks.Enqueue((callback, state));
            }
            CallbackPostedCompletion.TrySetResult();
        }
    }

    private sealed class NonPumpingSynchronizationContext : SynchronizationContext {
        public override void Post(SendOrPostCallback callback, object state) {
        }
    }

    private static async Task<Readiness> WaitForServer(Process process, string readyFile, CancellationToken token) {
        var json = await ProtocolTestServerInfrastructure.WaitForReadinessAsync(
            process,
            readyFile,
            TimeSpan.FromSeconds(30),
            token);
        return JsonSerializer.Deserialize<Readiness>(json, new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidDataException("The SFTP test server returned empty readiness data.");
    }

    private sealed class Readiness {
        public string Host { get; set; }
        public int SftpPort { get; set; }
        public string Ipv6Host { get; set; }
        public int SftpIpv6Port { get; set; }
        public bool PosixOnlyFileAvailable { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string KnownHostsPath { get; set; }
        public string RootPath { get; set; }
        public bool SymlinksAvailable { get; set; }
    }

    private sealed class ProvidedIO : IProvidedIO {
        public IEnumerable<IEntryStreamSet> StreamSets { get; }

        public ProvidedIO(IEntryStreamSet streamSet) {
            StreamSets = [streamSet];
        }
    }

    private sealed class ClientDisposalStreamSet : IEntryStreamSet {
        private string SourcePath { get; }
        private SftpClient Client { get; }

        public ClientDisposalStreamSet(string sourcePath, SftpClient client) {
            SourcePath = sourcePath ?? throw new ArgumentNullException(nameof(sourcePath));
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public IEntryStreamReady StreamSourceReady() {
            return new EntryStreamReady();
        }

        public IEnumerable<string> FileSource() {
            return [SourcePath];
        }

        public async IAsyncEnumerable<IEntryStreamSource> StreamSource(
            [EnumeratorCancellation] CancellationToken token) {
            token.ThrowIfCancellationRequested();
            yield return new ClientDisposalStreamSource(SourcePath, Client);
            await Task.CompletedTask;
        }
    }

    private sealed class ClientDisposalStreamSource : IEntryStreamSource {
        private string SourcePath { get; }

        public string EntryID => SourcePath;
        public string EntryName => Path.GetFileName(SourcePath);
        public string RelativePath => Path.GetFileName(SourcePath) + ".stream";
        public string SourceFile => SourcePath;
        public string SourceDirectory => null;
        public long StreamLength => Content.Length;
        public Stream Stream { get; }

        private byte[] Content { get; }

        public ClientDisposalStreamSource(string sourcePath, SftpClient client) {
            SourcePath = sourcePath ?? throw new ArgumentNullException(nameof(sourcePath));
            Content = Encoding.UTF8.GetBytes("dispose the SFTP client after the destination write");
            Stream = new ClientDisposalReadStream(Content, client);
        }

        public Task<IEntryStreamReady> StreamReady(CancellationToken token) {
            token.ThrowIfCancellationRequested();
            return Task.FromResult<IEntryStreamReady>(new EntryStreamReady());
        }
    }

    private sealed class ClientDisposalReadStream : Stream {
        private byte[] Content { get; }
        private SftpClient Client { get; }
        private int Offset;

        public sealed override bool CanRead => true;
        public sealed override bool CanSeek => false;
        public sealed override bool CanWrite => false;
        public sealed override long Length => Content.Length;
        public sealed override long Position {
            get => Offset;
            set => throw new NotSupportedException();
        }

        public ClientDisposalReadStream(byte[] content, SftpClient client) {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public sealed override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            if (Offset == Content.Length) {
                await Client.DisposeAsync();
                return 0;
            }
            var count = Math.Min(buffer.Length, Content.Length - Offset);
            Content.AsMemory(Offset, count).CopyTo(buffer);
            Offset += count;
            return count;
        }

        public sealed override int Read(byte[] buffer, int offset, int count) {
            ValidateBufferArguments(buffer, offset, count);
            return ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).GetAwaiter().GetResult();
        }

        public sealed override void Flush() {
        }

        public sealed override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public sealed override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public sealed override void Write(byte[] buffer, int offset, int count) {
            throw new NotSupportedException();
        }
    }

    private sealed class GateHoldingStreamSet : IEntryStreamSet {
        private string SourcePath { get; }
        private GateHoldingReadStream Stream { get; }

        public GateHoldingStreamSet(string sourcePath, GateHoldingReadStream stream) {
            SourcePath = sourcePath ?? throw new ArgumentNullException(nameof(sourcePath));
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        public IEntryStreamReady StreamSourceReady() {
            return new EntryStreamReady();
        }

        public IEnumerable<string> FileSource() {
            return [SourcePath];
        }

        public async IAsyncEnumerable<IEntryStreamSource> StreamSource(
            [EnumeratorCancellation] CancellationToken token) {
            token.ThrowIfCancellationRequested();
            yield return new GateHoldingStreamSource(SourcePath, Stream);
            await Task.CompletedTask;
        }
    }

    private sealed class GateHoldingStreamSource : IEntryStreamSource {
        private string SourcePath { get; }

        public string EntryID => SourcePath;
        public string EntryName => Path.GetFileName(SourcePath);
        public string RelativePath => Path.GetFileName(SourcePath) + ".stream";
        public string SourceFile => SourcePath;
        public string SourceDirectory => null;
        public long StreamLength => Stream.Length;
        public Stream Stream { get; }

        public GateHoldingStreamSource(string sourcePath, GateHoldingReadStream stream) {
            SourcePath = sourcePath ?? throw new ArgumentNullException(nameof(sourcePath));
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        public Task<IEntryStreamReady> StreamReady(CancellationToken token) {
            token.ThrowIfCancellationRequested();
            return Task.FromResult<IEntryStreamReady>(new EntryStreamReady());
        }
    }

    private sealed class GateHoldingReadStream : Stream {
        private readonly byte[] Content;
        private readonly SemaphoreSlim Gate;
        private readonly TaskCompletionSource GateAcquiredCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int Offset;
        private int GateHeld;

        public Task GateAcquired => GateAcquiredCompletion.Task;
        public sealed override bool CanRead => true;
        public sealed override bool CanSeek => false;
        public sealed override bool CanWrite => false;
        public sealed override long Length => Content.Length;
        public sealed override long Position {
            get => Offset;
            set => throw new NotSupportedException();
        }

        public GateHoldingReadStream(byte[] content, SemaphoreSlim gate) {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Gate = gate ?? throw new ArgumentNullException(nameof(gate));
        }

        public sealed override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            if (Offset == Content.Length) {
                await Gate.WaitAsync(cancellationToken);
                Volatile.Write(ref GateHeld, 1);
                GateAcquiredCompletion.TrySetResult();
                return 0;
            }
            var count = Math.Min(buffer.Length, Content.Length - Offset);
            Content.AsMemory(Offset, count).CopyTo(buffer);
            Offset += count;
            return count;
        }

        public void ReleaseGate() {
            if (Interlocked.Exchange(ref GateHeld, 0) != 0) {
                Gate.Release();
            }
        }

        public sealed override int Read(byte[] buffer, int offset, int count) {
            ValidateBufferArguments(buffer, offset, count);
            return ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).GetAwaiter().GetResult();
        }

        public sealed override void Flush() {
        }

        public sealed override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public sealed override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public sealed override void Write(byte[] buffer, int offset, int count) {
            throw new NotSupportedException();
        }
    }

    private sealed class CommandSource : ICommandSource {
        public object Item => Entries.FirstOrDefault();
        public IEnumerable Items { get; }

        private IReadOnlyList<SftpEntry> Entries { get; }

        public CommandSource(IReadOnlyList<SftpEntry> entries) {
            Entries = entries;
            Items = entries;
        }
    }
}
