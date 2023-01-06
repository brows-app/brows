using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO {
    using Gui;

    internal class Win32FileSystemEntry : FileSystemEntry {
        private Win32FileOpener Opener =>
            _Opener ?? (
            _Opener = new Win32FileOpener());
        private Win32FileOpener _Opener;

        private Win32FileLinkResolver LinkResolver => Win32Provider.FileLinkResolver;

        protected override IIconProvider IconProvider => Win32Provider.Icon;
        protected override IOverlayProvider OverlayProvider => Win32Provider.Overlay;
        protected override IPreviewProvider PreviewProvider => Win32Provider.Preview;
        protected override IThumbnailProvider ThumbnailProvider => Win32Provider.Thumbnail;
        protected override IFilePropertyProvider PropertyProvider => Kind == FileSystemEntryKind.File
            ? Win32Provider.Property
            : null;

        protected override async Task<bool> Open(CancellationToken cancellationToken) {
            if (Info is FileInfo) {
                var file = File;
                var link = await LinkResolver.Resolve(file, cancellationToken);
                if (link != null) {
                    var browsed = await Browse(link, cancellationToken);
                    if (browsed) {
                        return true;
                    }
                }
            }
            var opened = await base.Open(cancellationToken);
            if (opened == false) {
                var file = File;
                var open = Opener;
                await open.Open(file, cancellationToken);
            }
            return true;
        }

        public Win32FileSystemEntry(FileSystemInfo info, CancellationToken cancellationToken) : base(info, cancellationToken) {
        }
    }
}
