using Brows.Ftp;
using Brows.Url.Ftp;
using System;

namespace Brows {
    internal sealed class FtpEntry : FileProtocolEntry<FtpProvider> {
        public string RelativePath => _RelativePath ??= new Func<string>(() =>
            Uri.UnescapeDataString(
                Uri.AbsolutePath
                    .Substring(Provider.Uri.AbsolutePath.Length)
                    .TrimStart('/'))
        )();
        private string _RelativePath;

        public FtpListingInfo Info { get; }
        public sealed override Uri Uri { get; }
        public sealed override string Name { get; }
        public sealed override FileProtocolEntryKind Kind { get; }

        public FtpEntry(FtpProvider provider, FtpListingInfo info, Uri uri = null) : base(provider) {
            Info = info ?? throw new ArgumentNullException(nameof(info));
            Name = Info.Name;
            Kind = Info.EntryKind();
            Uri = uri ?? FileProtocolUri.Extend(Provider.Uri, [Name], includeTrailingSlash: Kind == FileProtocolEntryKind.Directory);
        }

        public FtpClient Client() {
            return Provider.ClientFor(this);
        }
    }
}
