using Domore.Threading;

namespace Brows.IO {
    using Gui;

    internal class Win32Provider {
        private static readonly STAThreadPool ThreadPool = new STAThreadPool(nameof(Win32Provider));

        public static IIconProvider Icon { get; } = new Win32IconProvider(ThreadPool);
        public static IOverlayProvider Overlay { get; } = new Win32OverlayProvider(ThreadPool);
        public static IPreviewProvider Preview { get; } = new Win32PreviewProvider(ThreadPool);
        public static IThumbnailProvider Thumbnail { get; } = new Win32ThumbnailProvider(ThreadPool);
        public static Win32FileLinkResolver FileLinkResolver { get; } = new Win32FileLinkResolver(ThreadPool);
        public static Win32FilePropertyProvider Property { get; } = new Win32FilePropertyProvider(ThreadPool);
    }
}
