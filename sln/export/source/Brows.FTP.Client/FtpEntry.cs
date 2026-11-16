using Brows.DirectoryListing;
using System;

namespace Brows;

internal sealed class FtpEntry : FileProtocolEntry<FtpProviderBase>, IFileProtocolResourceIdentity {
    internal new FtpProviderBase Provider => base.Provider;

    internal Uri DirectoryUri => FileProtocolUri.Extend(
        FileProtocolUri.Parent(Uri) ?? Provider.Uri,
        [Name],
        includeTrailingSlash: true);

    public string RelativePath => field ??= FileProtocolUri.RelativePath(Provider.Uri, Uri);
    public string RemotePath => field ??= FileProtocolUri.CombinePath(Provider.Uri, RelativePath);
    public FtpStreamGui Stream => new(this);

    public DirectoryListItem Info { get; }
    public sealed override Uri Uri { get; }
    public sealed override string Name { get; }
    public sealed override FileProtocolEntryKind Kind { get; }

    public FtpEntry(FtpProviderBase provider, DirectoryListItem info, Uri uri = null) : base(provider) {
        Info = info ?? throw new ArgumentNullException(nameof(info));
        Name = Info.Name;
        Kind = Info.EntryKind();
        Uri = uri ?? FileProtocolUri.Extend(Provider.Uri, [Name], includeTrailingSlash:
            Kind == FileProtocolEntryKind.Directory);
    }

    public FtpClient Client() {
        return Provider.ClientFor(this);
    }

    Uri IFileProtocolResourceIdentity.GetResourceIdentity(Uri uri) {
        return Provider.ResourceIdentity(uri);
    }
}
