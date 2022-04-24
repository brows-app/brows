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

        private Win32FileLinkResolver LinkResolver => Win32Gui.FileLinkResolver;

        protected override IIconProvider IconProvider => Win32Gui.IconProvider;
        protected override IPreviewProvider PreviewProvider => Win32Gui.PreviewProvider;
        protected override IThumbnailProvider ThumbnailProvider => Win32Gui.ThumbnailProvider;

        protected override async Task<bool> Open(CancellationToken cancellationToken) {
            if (Info is FileInfo) {
                var file = Info.FullName;
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
                var file = Info.FullName;
                var open = Opener;
                await open.Open(file, cancellationToken);
            }
            return true;
        }

        public Win32FileSystemEntry(FileSystemInfo info, CancellationToken cancellationToken) : base(info, cancellationToken) {
        }
    }
}
