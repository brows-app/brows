using Brows.Composition;
using Brows.Entries;
using Brows.SFTP.Native;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
internal sealed class SftpProviderTest {
    private sealed class FakeSftpFileOperations : ISftpFileOperations {
        public List<string> DeletedFiles { get; } = [];
        public List<string> DeletedDirectories { get; } = [];
        public List<(string Source, string Target)> Renames { get; } = [];

        public Task DeleteFile(string path, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            DeletedFiles.Add(path);
            return Task.CompletedTask;
        }

        public Task DeleteDirectory(string path, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            DeletedDirectories.Add(path);
            return Task.CompletedTask;
        }

        public Task Rename(string source, string destination, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            Renames.Add((source, destination));
            return Task.CompletedTask;
        }
    }

    private static T CreateProviderExport<T>(string name) where T : class {
        var type = typeof(SftpProvider).GetNestedType(name, BindingFlags.NonPublic);
        return Activator.CreateInstance(type, nonPublic: true) as T
            ?? throw new InvalidOperationException($"The SFTP provider export {name} could not be created.");
    }

    [Test]
    public async Task DeleteRecursive_DirectoryLinkDeletesLinkWithoutTraversingTarget() {
        var factory = new SftpProviderFactory();
        var uri = new Uri("sftp://user@127.0.0.1:2222/root/");
        using var lease = factory.Acquire(uri);
        var fileOperations = new FakeSftpFileOperations();
        using var provider = new SftpProvider(factory, uri, null, lease, fileOperations);
        var link = new SftpEntry(
            provider,
            new SftpMetadata("linked-directory", 4, 0, 0, 0, 0xA1FF, 0, 0),
            FileProtocolEntryKind.DirectoryLink);

        await provider.DeleteRecursive(link, CancellationToken.None);

        Assert.Multiple(() => {
            Assert.That(link.RemotePath, Is.EqualTo("/root/linked-directory/"));
            Assert.That(fileOperations.DeletedFiles, Is.EqualTo(new[] { "/root/linked-directory" }));
            Assert.That(fileOperations.DeletedFiles, Does.Not.Contain("/root/target-directory"));
            Assert.That(fileOperations.DeletedDirectories, Is.Empty);
        });
    }

    [Test]
    public async Task Rename_DirectoryLinkUsesMutationPathsAndPreservesNameWhitespace() {
        var factory = new SftpProviderFactory();
        var uri = new Uri("sftp://user@127.0.0.1:2222/root/");
        using var lease = factory.Acquire(uri);
        var fileOperations = new FakeSftpFileOperations();
        using var provider = new SftpProvider(factory, uri, null, lease, fileOperations);
        var nestedUri = FileProtocolUri.Extend(uri, ["nested"], includeTrailingSlash: true);
        var linkUri = FileProtocolUri.Extend(nestedUri, [" linked "], includeTrailingSlash: true);
        var link = new SftpEntry(
            provider,
            new SftpMetadata(" linked ", 4, 0, 0, 0, 0xA1FF, 0, 0),
            FileProtocolEntryKind.DirectoryLink,
            linkUri);
        await provider.Rename(link, " renamed ", CancellationToken.None);

        Assert.Multiple(() => {
            Assert.That(link.RemotePath, Is.EqualTo("/root/nested/ linked /"));
            Assert.That(
                fileOperations.Renames,
                Is.EqualTo(new[] { ("/root/nested/ linked ", "/root/nested/ renamed ") }));
        });
    }

    [Test]
    public void MutationPath_PreservesRootAndLiteralFilenameWhitespace() {
        var factory = new SftpProviderFactory();
        var uri = new Uri("sftp://user@127.0.0.1:2222/");
        using var lease = factory.Acquire(uri);
        using var provider = new SftpProvider(factory, uri, null, lease);
        var entry = new SftpEntry(
            provider,
            new SftpMetadata(" report ", 4, 0, 0, 0, 0x81A4, 0, 0),
            FileProtocolEntryKind.File);
        var directoryUri = FileProtocolUri.Extend(uri, ["directory"], includeTrailingSlash: true);

        Assert.Multiple(() => {
            Assert.That(provider.MutationPath(uri), Is.EqualTo("/"));
            Assert.That(entry.RemotePath, Is.EqualTo("/ report "));
            Assert.That(entry.MutationPath, Is.EqualTo("/ report "));
            Assert.That(provider.RemotePath(directoryUri), Is.EqualTo("/directory/"));
            Assert.That(provider.MutationPath(directoryUri), Is.EqualTo("/directory"));
        });
    }

    [Test]
    public async Task MoveProvidedIO_UnsupportedLocalReparsePointKeepsSourceTree() {
        var temporaryRoot = Path.Combine(Path.GetTempPath(), "brows-sftp-move-" + Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(temporaryRoot, "source");
        var targetRoot = Path.Combine(temporaryRoot, "target");
        var linkPath = Path.Combine(sourceRoot, "linked-directory");
        try {
            Directory.CreateDirectory(sourceRoot);
            Directory.CreateDirectory(targetRoot);
            await File.WriteAllTextAsync(Path.Combine(targetRoot, "child.txt"), "target contents");
            try {
                Directory.CreateSymbolicLink(linkPath, targetRoot);
            }
            catch (UnauthorizedAccessException error) {
                Assert.Ignore($"The host cannot create a directory symbolic link: {error.Message}");
            }
            catch (PlatformNotSupportedException error) {
                Assert.Ignore($"The host cannot create a directory symbolic link: {error.Message}");
            }
            catch (IOException error) when (error.Message.Contains("privilege", StringComparison.OrdinalIgnoreCase)) {
                Assert.Ignore($"The host cannot create a directory symbolic link: {error.Message}");
            }

            var factory = new SftpProviderFactory();
            var targetUri = new Uri("sftp://target@127.0.0.1:2222/target/");
            using var lease = factory.Acquire(targetUri);
            var targetProvider = new SftpProvider(factory, targetUri, null, lease);
            var move = CreateProviderExport<IWorkProvidedIO>("MoveProvidedIO");
            var provided = new ProvidedIO(EntryStreamSet.FromFiles([sourceRoot]));

            await Assert.ThrowsAsync<NotSupportedException>(async () => await move.Work(
                [provided],
                targetProvider,
                progress: null,
                CancellationToken.None));
            Assert.That(Directory.Exists(sourceRoot), Is.True);
            Assert.That(Directory.Exists(linkPath), Is.True);
            Assert.That(
                File.GetAttributes(linkPath).HasFlag(FileAttributes.ReparsePoint),
                Is.True);
            Assert.That(File.Exists(Path.Combine(targetRoot, "child.txt")), Is.True);
        }
        finally {
            var resolvedTemporaryRoot = Path.GetFullPath(temporaryRoot);
            var temporaryDirectory = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var temporaryRootIsWithinTempDirectory = resolvedTemporaryRoot.StartsWith(
                temporaryDirectory,
                StringComparison.OrdinalIgnoreCase);
            if (temporaryRootIsWithinTempDirectory && Directory.Exists(resolvedTemporaryRoot)) {
                Directory.Delete(resolvedTemporaryRoot, recursive: true);
            }
        }
    }

    private sealed class ProvidedIO : IProvidedIO {
        public IEnumerable<IEntryStreamSet> StreamSets { get; }

        public ProvidedIO(IEntryStreamSet streamSet) {
            StreamSets = [streamSet];
        }
    }
}
