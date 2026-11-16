using Brows.SFTP.Native;
using System;

namespace Brows;

internal sealed class SftpEntry : FileProtocolEntry<SftpProvider> {
    internal new SftpProvider Provider => base.Provider;

    public SftpMetadata Info { get; }
    public sealed override Uri Uri { get; }
    public sealed override string Name => Info.Name;
    public sealed override FileProtocolEntryKind Kind { get; }
    public string RelativePath => FileProtocolUri.RelativePath(Provider.Uri, Uri);
    internal string RemotePath => Provider.RemotePath(Uri);
    internal string MutationPath => Provider.MutationPath(Uri);
    internal SftpClient Client => Provider.Client;
    public SftpStreamGui Stream => new(this);

    internal SftpEntry(SftpProvider provider, SftpMetadata info, FileProtocolEntryKind kind, Uri uri = null) : base(provider) {
        Info = info ?? throw new ArgumentNullException(nameof(info));
        Kind = kind;
        Uri = uri ?? FileProtocolUri.Extend(provider.Uri, [Name], includeTrailingSlash:
            kind is FileProtocolEntryKind.Directory or FileProtocolEntryKind.DirectoryLink);
    }
}
