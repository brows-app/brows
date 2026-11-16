using Brows.SFTP.Native;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed class SftpProvider : FileProtocolProvider<SftpEntry, SftpConfig> {
    private const uint MetadataPermissionsFlag = 0x4;

    private static readonly ILog Log = Logging.For(typeof(SftpProvider));

    private async Task<SftpMetadata> CompleteMetadata(SftpMetadata metadata, string path, CancellationToken token) {
        if ((metadata.Flags & MetadataPermissionsFlag) != 0) {
            return metadata;
        }
        var info = await Client.Stat(path, link: true, token);
        return info with {
            Name = metadata.Name
        };
    }

    private async Task<FileProtocolEntryKind> EntryKind(SftpMetadata metadata, string path, CancellationToken token) {
        var mode = metadata.Permissions;
        if ((metadata.Flags & MetadataPermissionsFlag) == 0) {
            return FileProtocolEntryKind.File;
        }
        if (SftpPosixMode.IsDirectory(mode)) {
            return FileProtocolEntryKind.Directory;
        }
        if (!SftpPosixMode.IsSymbolicLink(mode)) {
            return FileProtocolEntryKind.File;
        }
        var target = await Client.StatLinkTarget(path, token);
        if (target.ErrorStatus is uint status) {
            if (status is not 2 and not 3 and not 10) {
                throw target.Error;
            }
            if (Log.Debug()) {
                Log.Debug(Log.Join("Unable to resolve SFTP link target", path, target.Error));
            }
            return FileProtocolEntryKind.FileLink;
        }
        return SftpPosixMode.IsDirectory(target.Metadata.Permissions)
            ? FileProtocolEntryKind.DirectoryLink
            : FileProtocolEntryKind.FileLink;
    }

    internal async IAsyncEnumerable<SftpEntry> ListEntries(Uri uri, [EnumeratorCancellation] CancellationToken token) {
        var path = ToRemotePath(uri);
        await using var directory = await Client.OpenDirectory(path, token);
        for (;;) {
            token.ThrowIfCancellationRequested();
            var metadata = await Client.ReadDirectory(directory, token);
            if (metadata is null) {
                yield break;
            }
            if (metadata.Name is "." or "..") {
                continue;
            }
            var itemUri = FileProtocolUri.Extend(uri, [metadata.Name], includeTrailingSlash: false);
            var itemPath = ToRemotePath(itemUri);
            metadata = await CompleteMetadata(metadata, itemPath, token);
            var kind = await EntryKind(metadata, itemPath, token);
            var entryUri = FileProtocolUri.Extend(uri,
                                                  [metadata.Name],
                                                  includeTrailingSlash: kind is FileProtocolEntryKind.Directory or FileProtocolEntryKind.DirectoryLink);
            yield return new SftpEntry(this, metadata, kind, entryUri);
        }
    }

    private static string ToRemotePath(Uri uri) {
        var root = new UriBuilder(uri) { Path = "/", Query = null, Fragment = null }.Uri;
        var relative = FileProtocolUri.RelativePath(root, uri);
        return relative.Length == 0
            ? "/"
            : "/" + relative;
    }

    private static string TrimMutationPath(string path) {
        return path.Length <= 1
            ? path
            : path.TrimEnd('/');
    }

    protected sealed override async Task List(CancellationToken token) {
        if (Log.Info()) {
            Log.Info(Log.Join("SFTP listing", FileProtocolUri.Redact(Uri)));
        }
        var count = 0;
        await foreach (var entry in ListEntries(Uri, token).WithCancellation(token)) {
            await Provide(entry, token);
            count++;
        }
        if (Log.Info()) {
            Log.Info(Log.Join("SFTP listed", FileProtocolUri.Redact(Uri), count));
        }
    }

    internal async Task DeleteRecursive(SftpEntry entry, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Kind == FileProtocolEntryKind.Directory) {
            await foreach (var child in ListEntries(entry.Uri, token).WithCancellation(token)) {
                await DeleteRecursive(child, token);
            }
            await FileOperations.DeleteDirectory(entry.MutationPath, token);
            return;
        }
        await FileOperations.DeleteFile(entry.MutationPath, token);
    }

    internal Task DeleteEntry(SftpEntry entry, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.Kind == FileProtocolEntryKind.Directory
            ? FileOperations.DeleteDirectory(entry.MutationPath, token)
            : FileOperations.DeleteFile(entry.MutationPath, token);
    }

    public object Icon { get; }
    public SftpProviderFactory Factory { get; }
    internal SftpClient Client { get; }
    internal EntryStreamGuiOptions StreamOptions => Config?.Stream ?? (field ??= new());

    internal string RemotePath(Uri uri) {
        ArgumentNullException.ThrowIfNull(uri);
        return ToRemotePath(uri);
    }

    internal string MutationPath(Uri uri) {
        ArgumentNullException.ThrowIfNull(uri);
        return TrimMutationPath(ToRemotePath(uri));
    }

    internal Task Rename(SftpEntry entry, string name, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(name);
        var parentUri = FileProtocolUri.Parent(entry.Uri) ?? Uri;
        var targetIsDirectory = entry.Kind is FileProtocolEntryKind.Directory or FileProtocolEntryKind.DirectoryLink;
        var targetUri = FileProtocolUri.Extend(parentUri, [name], includeTrailingSlash: targetIsDirectory);
        return RenameToPath(entry, MutationPath(targetUri), token);
    }

    internal Task RenameToPath(SftpEntry entry, string targetPath, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(targetPath);
        return FileOperations.Rename(entry.MutationPath, TrimMutationPath(targetPath), token);
    }

    internal SftpProvider(SftpProviderFactory factory,
                          Uri uri,
                          object icon,
                          SftpClientLease lease,
                          ISftpFileOperations fileOperations = null)
        : base(uri, 100, StringComparer.Ordinal, StringComparer.Ordinal) {
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        Icon = icon;
        Lease = lease ?? throw new ArgumentNullException(nameof(lease));
        Client = lease.Client;
        FileOperations = fileOperations ?? Client;
    }

    private ISftpFileOperations FileOperations { get; }
    private SftpClientLease Lease { get; }

    protected sealed override void End() {
        Lease.Dispose();
        base.End();
    }

    private sealed class ProvideIO : ProvideIO<SftpProvider> {
        protected sealed override async IAsyncEnumerable<SftpEntry> List(SftpProvider provider,
                                                                          Uri uri,
                                                                          [EnumeratorCancellation] CancellationToken token) {
            ArgumentNullException.ThrowIfNull(provider);
            await foreach (var item in provider.ListEntries(uri, token).WithCancellation(token)) {
                yield return item;
            }
        }

        protected sealed override FileProtocolStreamSource<SftpEntry> Read(SftpProvider provider, SftpEntry entry) {
            return new SftpStreamSource(entry);
        }
    }

    private sealed class CopyProvidedIO : CopyProvidedIO<SftpProvider> {
        protected sealed override Task CreateDirectory(SftpProvider provider, string path, CancellationToken token) {
            return provider.Client.CreateDirectoryIfMissing(path, token);
        }

        protected sealed override Task<Stream> Write(SftpProvider provider,
                                                     string path,
                                                     long length,
                                                     CancellationToken token) {
            return provider.Client.OpenWrite(path, token);
        }
    }

    private sealed class MoveProvidedIO : IMoveProvidedIO, IProviderExport<SftpProvider> {
        private static readonly CopyProvidedIO Copy = new();

        async Task<bool> IWorkProvidedIO.Work(IEnumerable<IProvidedIO> io,
                                               IProvider target,
                                               IOperationProgress progress,
                                               CancellationToken token) {
            if (io is null) {
                throw new ArgumentNullException(nameof(io));
            }
            if (target is not SftpProvider provider) {
                return false;
            }
            var providedIO = io.ToList();
            var sources = providedIO.FileSources();
            if (sources.Count == 0) {
                return false;
            }
            var snapshot = LocalFileMove.Capture(sources, token);
            var copied = await ((ICopyProvidedIO)Copy).Work(providedIO, provider, progress, token);
            if (!copied) {
                return false;
            }
            LocalFileMove.DeleteUnchanged(
                sources,
                snapshot,
                "The local source changed during the move; the source was retained.",
                token);
            return true;
        }
    }
}
