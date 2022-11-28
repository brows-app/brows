using System.IO;
using System.Threading;

namespace Brows.IO {
    using Gui;

    internal class Win32DriveEntry : DriveEntry {
        protected override IIconProvider IconProvider => Win32Gui.IconProvider;
        protected override IOverlayProvider OverlayProvider => Win32Gui.OverlayProvider;
        protected override IPreviewProvider PreviewProvider => Win32Gui.PreviewProvider;
        protected override IThumbnailProvider ThumbnailProvider => Win32Gui.ThumbnailProvider;

        public Win32DriveEntry(DriveInfo info, CancellationToken cancellationToken) : base(info, cancellationToken) {
        }
    }
}
