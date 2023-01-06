using System.IO;
using System.Threading;

namespace Brows.IO {
    using Gui;

    internal class Win32DriveEntry : DriveEntry {
        protected override IIconProvider IconProvider => Win32Provider.Icon;
        protected override IOverlayProvider OverlayProvider => Win32Provider.Overlay;
        protected override IPreviewProvider PreviewProvider => Win32Provider.Preview;
        protected override IThumbnailProvider ThumbnailProvider => Win32Provider.Thumbnail;

        public Win32DriveEntry(DriveInfo info, CancellationToken cancellationToken) : base(info, cancellationToken) {
        }
    }
}
