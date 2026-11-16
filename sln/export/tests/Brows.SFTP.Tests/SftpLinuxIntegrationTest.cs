using Brows.Commands;
using Brows.Composition;
using Brows.Entries;
using Brows.Native;
using Brows.Operations;
using Brows.Panels;
using Brows.Providers;
using Brows.SFTP.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
[Category("Integration")]
[NonParallelizable]
internal sealed class SftpLinuxIntegrationTest {
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

    private static Uri CreateUri(Readiness readiness) {
        return new UriBuilder("sftp", readiness.Host, readiness.SftpPort) {
            UserName = readiness.Username,
            Path = readiness.FixturePath.TrimEnd('/') + "/"
        }.Uri;
    }

    private static void InitializeProvider(SftpProvider provider, IImportAgent import) {
        typeof(Provider)
            .GetProperty("Import", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(provider, import);
        var config = new SftpConfig();
        for (var type = provider.GetType(); type is not null; type = type.BaseType) {
            type.GetProperty("Config", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                ?.SetValue(provider, config);
        }
    }

    private static IImportAgent CreateProviderImport() {
        var import = new Mock<IImportAgent>();
        import.SetupGet(value => value.Ready).Returns(true);
        import.Setup(value => value.List<IEntryDataExport>(It.IsAny<bool>())).Returns([]);
        import.Setup(value => value.List<IExport>(It.IsAny<bool>())).Returns([]);
        return import.Object;
    }

    private static bool TriggerMove(SftpProvider source, IProvider target, ICopyProvidedIO copy,
        IReadOnlyList<SftpEntry> entries, CancellationToken token) {
        var command = new Move();
        InitializeCommand(command);
        var active = new TestPanel {
            Column = 0,
            Provider = source,
            Services = { CreateProviderExport<IProvideIO>("ProvideIO") }
        };
        var passive = new TestPanel { Column = 1, Provider = target, Services = { copy } };
        var selection = new TestCommandSource();
        foreach (var entry in entries) {
            selection.Items.Add(entry);
        }
        return ((ICommand)command).TriggeredWork(new TestCommandContext {
            Line = new TestCommandLine { Parameter = "auto" },
            Panels = { Panels = { active, passive } },
            Source = selection,
            Token = token
        });
    }

    private static async Task AssertChangedMoveSourcesAreRetained(SftpProvider provider,
        string rootPath, string localRoot, CancellationToken token) {
        var client = provider.Client;
        using var target = new FileSystemTestDestination(Path.Combine(localRoot, "changed-move-target"));
        foreach (var change in new[] { "child", "file", "link" }) {
            var sourcePath = rootPath + "/move-change-" + change;
            await client.CreateDirectory(sourcePath, token);
            await WriteRemoteText(client, sourcePath + "/target.txt", "original source", token);
            var rootUri = new UriBuilder(provider.Uri) { Path = rootPath + "/" }.Uri;
            var entry = await FindEntry(provider, rootUri, "move-change-" + change, token);
            var copy = new MutatingCopy(async copyToken => {
                switch (change) {
                    case "child":
                        await WriteRemoteText(client, sourcePath + "/late.txt", "never copied", copyToken);
                        break;
                    case "file":
                        await WriteRemoteText(client, sourcePath + "/target.txt", "changed after copying", copyToken);
                        break;
                    case "link":
                        await client.Rename(rootPath + "/file-link", sourcePath + "/late-link", copyToken);
                        break;
                }
            });
            var error = Assert.Catch<Exception>(() => TriggerMove(provider, target.Provider, copy, [entry], token));
            Assert.That(copy.Copied, Is.EqualTo(new[] { entry.RelativePath.TrimEnd('/') + "/target.txt" }));
            if (change == "link") {
                Assert.That(error, Is.TypeOf<NotSupportedException>());
                var metadata = await client.Stat(sourcePath + "/late-link", link: true, token);
                Assert.That(SftpPosixMode.IsSymbolicLink(metadata.Permissions), Is.True);
            }
            else {
                Assert.That(error, Is.TypeOf<IOException>());
            }
            var expected = change == "file" ? "changed after copying" : "original source";
            Assert.That(await ReadRemoteText(client, sourcePath + "/target.txt", token), Is.EqualTo(expected));
            if (change == "child") {
                Assert.That(await ReadRemoteText(client, sourcePath + "/late.txt", token), Is.EqualTo("never copied"));
            }
        }
    }

    private static async Task AssertInvalidFilenameMovesRetainSources(SftpProvider provider,
        string rootPath, string localRoot, CancellationToken token) {
        var client = provider.Client;
        foreach (var mixed in new[] { false, true }) {
            var sourcePath = rootPath + (mixed ? "/mixed-names" : "/space-only-name");
            await client.CreateDirectory(sourcePath, token);
            await WriteRemoteText(client, sourcePath + "/ ", "literal space filename", token);
            if (mixed) {
                await WriteRemoteText(client, sourcePath + "/valid.txt", "also retain this file", token);
            }
            var sourceUri = new UriBuilder(provider.Uri) { Path = sourcePath + "/" }.Uri;
            using var source = new SftpProvider(provider.Factory, sourceUri, null,
                provider.Factory.Acquire(sourceUri, client.KnownHostsFile));
            InitializeProvider(source, CreateProviderImport());
            var entries = new List<SftpEntry>();
            await foreach (var entry in source.ListEntries(sourceUri, token)) {
                entries.Add(entry);
            }
            entries.Sort((first, second) => string.Compare(second.Name, first.Name, StringComparison.Ordinal));
            var targetPath = Path.Combine(localRoot, mixed ? "mixed-target" : "space-only-target");
            using var target = new FileSystemTestDestination(targetPath);

            Assert.Throws<InvalidDataException>(() => TriggerMove(source, target.Provider, target.Copy, entries, token));

            Assert.That(Directory.GetFileSystemEntries(targetPath), Is.Empty);
            Assert.That(await ReadRemoteText(client, sourcePath + "/ ", token), Is.EqualTo("literal space filename"));
            if (mixed) {
                Assert.That(await ReadRemoteText(client, sourcePath + "/valid.txt", token),
                    Is.EqualTo("also retain this file"));
            }
        }
    }

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
        typeof(Command)
            .GetProperty("CommandCliProvider", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(command, provider);
    }

    private static bool Trigger(ICommand command,
                                SftpProvider provider,
                                object item,
                                string name,
                                string parameter = null,
                                CancellationToken token = default) {
        InitializeCommand((Command)command);
        var panels = new TestPanelCollection();
        panels.Panels.Add(new TestPanel { Column = 0, Provider = provider });
        var source = new TestCommandSource();
        if (item is not null) {
            source.Items.Add(item);
        }
        var context = new TestCommandContext {
            Panels = panels,
            Source = source,
            Line = new TestCommandLine { Command = name, Parameter = parameter },
            Token = token
        };
        return command.TriggeredWork(context);
    }

    private static async Task<SftpEntry> FindEntry(
        SftpProvider provider,
        Uri directory,
        string name,
        CancellationToken token) {
        await foreach (var entry in provider.ListEntries(directory, token)) {
            if (string.Equals(entry.Name, name, StringComparison.Ordinal)) {
                return entry;
            }
        }
        throw new FileNotFoundException($"The SFTP entry {name} was not listed in {directory}.");
    }

    private static async Task<string> ReadRemoteText(SftpClient client, string path, CancellationToken token) {
        await using var remote = await client.OpenRead(path, token);
        using var content = new MemoryStream();
        await remote.CopyToAsync(content, token);
        return Encoding.UTF8.GetString(content.ToArray());
    }

    private static async Task WriteRemoteText(
        SftpClient client,
        string path,
        string content,
        CancellationToken token) {
        await using var remote = await client.OpenWrite(path, token);
        await remote.WriteAsync(Encoding.UTF8.GetBytes(content), token);
    }

    private static async Task RunFixtureHelper(string python, string repositoryRoot, params string[] arguments) {
        var helperPath = Path.Combine(
            repositoryRoot,
            "scripts",
            "protocol-test-servers",
            "linux_sftp_fixture.py");
        var start = new ProcessStartInfo(python) {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = repositoryRoot
        };
        start.ArgumentList.Add(helperPath);
        foreach (var argument in arguments) {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("The Linux SFTP fixture helper could not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try {
            await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (TimeoutException timeout) {
            try {
                if (!process.HasExited) {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch {
                try {
                    if (!process.HasExited) {
                        process.Kill();
                    }
                }
                catch {
                }
            }
            try {
                await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch {
            }
            try {
                await Task.WhenAll(output, error).WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch {
            }
            throw new TimeoutException("The Linux SFTP fixture helper exceeded 30 seconds.", timeout);
        }
        var details = (await output) + (await error);
        if (process.ExitCode != 0) {
            throw new InvalidOperationException($"The Linux SFTP fixture helper failed: {details}");
        }
    }

    private static void RemoveOpenedTemporaryFile(string path) {
        if (string.IsNullOrWhiteSpace(path)) {
            return;
        }

        var directoryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Brows", "SFTP"));
        var fullPath = Path.GetFullPath(path);
        var rootPrefix = Path.EndsInDirectorySeparator(directoryRoot)
            ? directoryRoot
            : directoryRoot + Path.DirectorySeparatorChar;
        var directory = Path.GetDirectoryName(fullPath);
        var pathIsOwned = fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) &&
                          directory is not null &&
                          Guid.TryParseExact(Path.GetFileName(directory), "N", out _);
        if (!pathIsOwned) {
            throw new InvalidOperationException($"Refusing to remove an unexpected open-file path: {fullPath}");
        }

        if (File.Exists(fullPath)) {
            File.SetAttributes(fullPath, FileAttributes.Normal);
            File.Delete(fullPath);
        }
        if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length == 0) {
            Directory.Delete(directory);
        }
    }

    private static void RemoveLocalRoot(string path) {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) {
            return;
        }

        var fullPath = Path.GetFullPath(path);
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        var tempPrefix = Path.EndsInDirectorySeparator(tempRoot)
            ? tempRoot
            : tempRoot + Path.DirectorySeparatorChar;
        var rootName = Path.GetFileName(fullPath);
        var nameIsOwned = rootName.StartsWith("brows-sftp-linux-", StringComparison.OrdinalIgnoreCase) &&
                          Guid.TryParseExact(rootName["brows-sftp-linux-".Length..], "N", out _);
        if (!fullPath.StartsWith(tempPrefix, StringComparison.OrdinalIgnoreCase) || !nameIsOwned) {
            throw new InvalidOperationException($"Refusing to remove an unexpected local test path: {fullPath}");
        }
        Directory.Delete(fullPath, recursive: true);
    }

    private static string RemoteJoin(params string[] segments) {
        return "/" + string.Join('/', segments.Select(segment => segment.Trim('/')));
    }

    [Test]
    public async Task LinuxOpenSsh_ExercisesBrowsAuthenticationCommandsAndRecursiveTransfers() {
        var readyFile = Environment.GetEnvironmentVariable("BROWS_SFTP_LINUX_READY_FILE");
        if (string.IsNullOrWhiteSpace(readyFile)) {
            Assert.Ignore("Set BROWS_SFTP_LINUX_READY_FILE to a Linux OpenSSH readiness file to run this fixture.");
        }
        if (!File.Exists(readyFile)) {
            Assert.Fail($"The configured Linux OpenSSH readiness file does not exist: {readyFile}");
        }

        var readinessText = await File.ReadAllTextAsync(readyFile);
        var readiness = JsonSerializer.Deserialize<Readiness>(readinessText, new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidDataException("The Linux OpenSSH readiness file is empty.");
        if (readiness.SftpPort <= 0 ||
            string.IsNullOrWhiteSpace(readiness.Host) ||
            string.IsNullOrWhiteSpace(readiness.Username) ||
            string.IsNullOrWhiteSpace(readiness.Password) ||
            string.IsNullOrWhiteSpace(readiness.KnownHostsPath) ||
            string.IsNullOrWhiteSpace(readiness.FixturePath)) {
            Assert.Fail("The Linux OpenSSH readiness file is missing required SFTP connection fields.");
        }

        var repositoryRoot = RepositoryRoot();
        var integrationRoot = Environment.GetEnvironmentVariable("BROWS_SFTP_LINUX_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(integrationRoot)) {
            integrationRoot = readiness.FixturePath.TrimEnd('/') +
                "/brows-linux-integration-" + Guid.NewGuid().ToString("N");
        }
        var integrationRootPrefix = readiness.FixturePath.TrimEnd('/') + "/brows-linux-integration-";
        var integrationRootName = integrationRoot.StartsWith(integrationRootPrefix, StringComparison.Ordinal)
            ? integrationRoot[integrationRootPrefix.Length..]
            : string.Empty;
        if (!Guid.TryParseExact(integrationRootName, "N", out _)) {
            Assert.Fail("BROWS_SFTP_LINUX_TEST_ROOT must be a generated child of fixturePath.");
        }

        var temporaryRoot = Path.Combine(Path.GetTempPath(), "brows-sftp-linux-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);
        using var operationTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var token = operationTimeout.Token;
        var knownHostsPath = Path.Combine(temporaryRoot, "known_hosts");
        File.WriteAllText(knownHostsPath, string.Empty);
        var uri = CreateUri(readiness);
        var native = new SftpNative();
        var factory = new SftpProviderFactory();
        SftpClientLease lease = null;
        SftpProvider provider = null;
        SftpProvider transferTarget = null;
        SftpProvider failedTarget = null;
        string openedPath = null;
        var remoteRootCreated = false;
        Exception testFailure = null;
        try {
            Assert.That(await native.Loaded(token), Is.True);
            lease = factory.Acquire(uri, knownHostsPath);
            provider = new SftpProvider(factory, uri, null, lease);
            InitializeProvider(provider, CreateProviderImport());
            var client = provider.Client;
            await client.Connect(token);
            Assert.That(client.KnownHostState, Is.EqualTo(SftpKnownHostStatus.NotFound));
            using var password = Secure(readiness.Password);
            Assert.That(await client.AuthenticateByPassword(password, token), Is.False);
            Assert.That(await client.TrustHostKey(token), Is.True);
            Assert.That(client.KnownHostState, Is.EqualTo(SftpKnownHostStatus.Match));
            Assert.That(await client.AuthenticateByPassword(password, token), Is.True);

            Assert.That(readiness.PosixOnlyFileAvailable, Is.True);
            var fixtureEntries = new List<SftpEntry>();
            await foreach (var entry in provider.ListEntries(provider.Uri, token)) {
                fixtureEntries.Add(entry);
            }
            var posixEntry = fixtureEntries.SingleOrDefault(entry => entry.Name == "colon:name.txt");
            Assert.That(posixEntry, Is.Not.Null);
            var opener = new CapturingOpenFile();
            var open = new Open { OpenFile = opener };
            Assert.That(Trigger(open, provider, posixEntry, "Sftp_Open", token: token), Is.True);
            openedPath = opener.Path;
            Assert.That(openedPath, Is.Not.Null.And.Not.Empty);
            Assert.That(await File.ReadAllTextAsync(openedPath, token), Is.EqualTo("POSIX filename fixture\n"));

            var rootPath = integrationRoot;
            await client.CreateDirectory(rootPath, token);
            remoteRootCreated = true;
            await client.CreateDirectory(rootPath + "/target-directory", token);
            await WriteRemoteText(
                client,
                rootPath + "/target-directory/target-child.txt",
                "directory link target\n",
                token);
            await WriteRemoteText(client, rootPath + "/target.txt", "file link target\n", token);
            await client.CreateDirectory(rootPath + "/recursive-source", token);
            await client.CreateDirectory(rootPath + "/recursive-source/nested", token);
            await client.CreateDirectory(rootPath + "/recursive-source/empty", token);
            await WriteRemoteText(
                client,
                rootPath + "/recursive-source/nested/child.txt",
                "recursive Linux transfer\n",
                token);

            var python = Python(repositoryRoot);
            await RunFixtureHelper(
                python,
                repositoryRoot,
                "--ready-file",
                Path.GetFullPath(readyFile),
                "--prepare-links",
                rootPath);

            var rootUri = new UriBuilder(provider.Uri) { Path = rootPath.TrimEnd('/') + "/" }.Uri;
            var directoryLink = await FindEntry(provider, rootUri, "directory-link", token);
            var fileLink = await FindEntry(provider, rootUri, "file-link", token);
            Assert.That(directoryLink.Kind, Is.EqualTo(FileProtocolEntryKind.DirectoryLink));
            Assert.That(fileLink.Kind, Is.EqualTo(FileProtocolEntryKind.FileLink));

            Assert.That(
                Trigger(new Rename(), provider, directoryLink, "Sftp_Rename", "directory-link-renamed", token),
                Is.True);
            var renamedLink = await FindEntry(provider, rootUri, "directory-link-renamed", token);
            Assert.That(renamedLink.Kind, Is.EqualTo(FileProtocolEntryKind.DirectoryLink));
            Assert.That(Trigger(new Delete(), provider, renamedLink, "Sftp_Delete", token: token), Is.True);
            Assert.That(
                SftpPosixMode.IsDirectory(
                    (await client.Stat(rootPath + "/target-directory", false, token)).Permissions),
                Is.True);
            Assert.That(
                await ReadRemoteText(client, rootPath + "/target-directory/target-child.txt", token),
                Is.EqualTo("directory link target\n"));

            Assert.That(
                Trigger(new Rename(), provider, fileLink, "Sftp_Rename", "file-link-renamed", token),
                Is.True);
            var renamedFileLink = await FindEntry(provider, rootUri, "file-link-renamed", token);
            Assert.That(renamedFileLink.Kind, Is.EqualTo(FileProtocolEntryKind.FileLink));
            Assert.That(Trigger(new Delete(), provider, renamedFileLink, "Sftp_Delete", token: token), Is.True);
            Assert.That(
                await ReadRemoteText(client, rootPath + "/target.txt", token),
                Is.EqualTo("file link target\n"));

            var sourceEntry = await FindEntry(provider, rootUri, "recursive-source", token);
            var targetPath = rootPath + "/copy-target";
            await client.CreateDirectory(targetPath, token);
            var targetUri = new UriBuilder(provider.Uri) { Path = targetPath + "/" }.Uri;
            transferTarget = new SftpProvider(factory, targetUri, null, factory.Acquire(uri, knownHostsPath));
            InitializeProvider(transferTarget, CreateProviderImport());
            var provided = new List<IProvidedIO>();
            var provide = CreateProviderExport<IProvideIO>("ProvideIO");
            var commandSource = new TestCommandSource();
            commandSource.Items.Add(sourceEntry);
            Assert.That(
                await provide.Work(provided, commandSource, provider, null, token),
                Is.True);
            var copy = CreateProviderExport<IWorkProvidedIO>("CopyProvidedIO");
            Assert.That(await copy.Work(provided, transferTarget, null, token), Is.True);

            var copiedRootPath = RemoteJoin(targetPath, sourceEntry.RelativePath);
            Assert.That(
                await ReadRemoteText(client, RemoteJoin(copiedRootPath, "nested", "child.txt"), token),
                Is.EqualTo("recursive Linux transfer\n"));
            Assert.That(
                SftpPosixMode.IsDirectory(
                    (await client.Stat(RemoteJoin(copiedRootPath, "empty"), false, token)).Permissions),
                Is.True);

            await RunFixtureHelper(python, repositoryRoot, "--ready-file", Path.GetFullPath(readyFile),
                "--prepare-links", rootPath);
            await AssertChangedMoveSourcesAreRetained(provider, rootPath, temporaryRoot, token);
            await AssertInvalidFilenameMovesRetainSources(provider, rootPath, temporaryRoot, token);

            var move = CreateProviderExport<IWorkProvidedIO>("MoveProvidedIO");
            var canceledSource = Path.Combine(temporaryRoot, "cancelled-move-source.txt");
            await File.WriteAllTextAsync(canceledSource, "keep after cancellation", token);
            using (var cancellation = new CancellationTokenSource()) {
                cancellation.Cancel();
                var canceledMove = await Assert.CatchAsync<OperationCanceledException>(async () => {
                    await move.Work(
                        [new ProvidedIO(EntryStreamSet.FromFiles([canceledSource]))],
                        transferTarget,
                        null,
                        cancellation.Token);
                });
                Assert.That(canceledMove, Is.Not.Null);
            }
            Assert.That(File.Exists(canceledSource), Is.True);

            var inFlightSource = Path.Combine(temporaryRoot, "in-flight-move-source.txt");
            var inFlightContents = new string('x', 1024 * 1024);
            await File.WriteAllTextAsync(inFlightSource, inFlightContents, token);
            using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token)) {
                var progress = new CancelAfterProgress(cancellation);
                var inFlightMove = await Assert.CatchAsync<OperationCanceledException>(async () => {
                    await move.Work(
                        [new ProvidedIO(EntryStreamSet.FromFiles([inFlightSource]))],
                        transferTarget,
                        progress,
                        cancellation.Token);
                });
                Assert.That(inFlightMove, Is.Not.Null);
            }
            Assert.That(File.ReadAllText(inFlightSource), Is.EqualTo(inFlightContents));
            var partialUploadPath = RemoteJoin(targetPath, Path.GetFileName(inFlightSource));
            var partialUpload = await client.Stat(partialUploadPath, link: false, token);
            Assert.That(partialUpload.Size, Is.GreaterThan(0));
            Assert.That(partialUpload.Size, Is.LessThan((ulong)Encoding.UTF8.GetByteCount(inFlightContents)));

            var failedSource = Path.Combine(temporaryRoot, "failed-move-source.txt");
            await File.WriteAllTextAsync(failedSource, "keep after remote failure", token);
            var failedUri = new UriBuilder(provider.Uri) { Path = rootPath + "/target.txt/" }.Uri;
            failedTarget = new SftpProvider(factory, failedUri, null, factory.Acquire(uri, knownHostsPath));
            InitializeProvider(failedTarget, CreateProviderImport());
            var failedMove = await Assert.CatchAsync<NativeErrorException>(async () => {
                await move.Work(
                    [new ProvidedIO(EntryStreamSet.FromFiles([failedSource]))],
                    failedTarget,
                    null,
                    token);
            });
            Assert.That(failedMove, Is.Not.Null);
            Assert.That(await File.ReadAllTextAsync(failedSource, token), Is.EqualTo("keep after remote failure"));
        }
        catch (Exception error) {
            testFailure = error;
            throw;
        }
        finally {
            using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var cleanupToken = cleanupTimeout.Token;
            var cleanupErrors = new List<Exception>();
            try {
                if (remoteRootCreated && provider is not null) {
                    var client = provider.Client;
                    var metadata = await client.Stat(integrationRoot, link: false, cleanupToken);
                    var rootUri = new UriBuilder(provider.Uri) { Path = integrationRoot.TrimEnd('/') + "/" }.Uri;
                    var entry = new SftpEntry(provider, metadata, FileProtocolEntryKind.Directory, rootUri);
                    await provider.DeleteRecursive(entry, cleanupToken);
                }
            }
            catch (Exception error) {
                cleanupErrors.Add(error);
            }
            try {
                RemoveOpenedTemporaryFile(openedPath);
            }
            catch (Exception error) {
                cleanupErrors.Add(error);
            }
            try {
                RemoveLocalRoot(temporaryRoot);
            }
            catch (Exception error) {
                cleanupErrors.Add(error);
            }
            try {
                failedTarget?.Dispose();
            }
            catch (Exception error) {
                cleanupErrors.Add(error);
            }
            try {
                transferTarget?.Dispose();
            }
            catch (Exception error) {
                cleanupErrors.Add(error);
            }
            try {
                provider?.Dispose();
                lease?.Dispose();
            }
            catch (Exception error) {
                cleanupErrors.Add(error);
            }
            try {
                await native.Freed(cleanupToken);
            }
            catch (Exception error) {
                cleanupErrors.Add(error);
            }
            if (cleanupErrors.Count > 0) {
                if (testFailure is not null) {
                    TestContext.Error.WriteLine(
                        "Linux SFTP integration cleanup also failed: " +
                        new AggregateException(cleanupErrors));
                }
                else {
                    throw new AggregateException("Linux SFTP integration cleanup failed.", cleanupErrors);
                }
            }
        }
    }

    private sealed class MutatingCopy(Func<CancellationToken, Task> mutate) : ICopyProvidedIO {
        internal List<string> Copied { get; } = [];

        public async Task<bool> Work(IEnumerable<IProvidedIO> io, IProvider target,
            IOperationProgress progress, CancellationToken token) {
            foreach (var provided in io) {
                foreach (var set in provided.StreamSets) {
                    await set.Consume(async (source, stream, _, copyToken) => {
                        if (stream is not null) {
                            await stream.CopyToAsync(Stream.Null, copyToken);
                            Copied.Add(source.RelativePath);
                        }
                    }, progress, token);
                }
            }
            await mutate(token);
            return true;
        }
    }

    private sealed class Readiness {
        public string Host { get; set; }
        public int SftpPort { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string KnownHostsPath { get; set; }
        public string FixturePath { get; set; }
        public bool PosixOnlyFileAvailable { get; set; }
    }

    private sealed class CapturingOpenFile : IOpenFile {
        public string Path { get; private set; }

        public Task<bool> Work(string file, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            Path = file;
            return Task.FromResult(true);
        }
    }

    private sealed class ProvidedIO : IProvidedIO {
        public IEnumerable<IEntryStreamSet> StreamSets { get; }

        public ProvidedIO(IEntryStreamSet streamSet) {
            StreamSets = [streamSet];
        }
    }

    private sealed class CancelAfterProgress : IOperationProgress {
        private readonly CancellationTokenSource Cancellation;
        private bool Cancelled;
        private bool TransferStarted;

        public CancelAfterProgress(CancellationTokenSource cancellation) {
            Cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
        }

        public void Change(long? addProgress = null,
                           long? setProgress = null,
                           long? addTarget = null,
                           long? setTarget = null,
                           string progressString = null,
                           string targetString = null,
                           string name = null,
                           string data = null,
                           OperationProgressKind? kind = null,
                           string progressFormat = null,
                           string targetFormat = null,
                           string progressRateFormat = null,
                           string progressRateAverageFormat = null,
                           System.IFormatProvider formatProvider = null) {
            var transferTargetWasSet = setProgress == 0 && setTarget.GetValueOrDefault() > 0;
            if (transferTargetWasSet) {
                TransferStarted = true;
            }
            var receivedBytes = TransferStarted && addProgress.GetValueOrDefault() > 0;
            if (receivedBytes && !Cancelled) {
                Cancelled = true;
                Cancellation.Cancel();
            }
        }

        public async Task<OperationResult> Child(
            string name,
            OperationDelegate task,
            OperationFailureBehavior failureBehavior,
            CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                ArgumentNullException.ThrowIfNull(task);
                await task(this, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return new OperationResult(OperationResultKind.Succeeded, null);
            }
            catch (Exception error) {
                cancellationToken.ThrowIfCancellationRequested();
                if (failureBehavior == OperationFailureBehavior.Throw) {
                    throw;
                }
                var kind = error is OperationCanceledException ? OperationResultKind.Canceled : OperationResultKind.Failed;
                return new OperationResult(kind, error);
            }
        }

        public Task<OperationBatchResult> Children<T>(
            IEnumerable<T> source,
            Func<T, OperationChild> child,
            OperationFailureBehavior failureBehavior,
            CancellationToken cancellationToken) {
            throw new NotSupportedException();
        }
    }
}
