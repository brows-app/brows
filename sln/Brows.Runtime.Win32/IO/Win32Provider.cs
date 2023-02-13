using Domore.Threading;

namespace Brows.IO {
    using Gui;

    internal static class Win32Provider {
        private static readonly STAThreadPool ThreadPool = new STAThreadPool(nameof(Win32Provider));

        public static Win32IconProvider Icon { get; } = new Win32IconProvider(ThreadPool);
        public static Win32OverlayProvider Overlay { get; } = new Win32OverlayProvider(ThreadPool);
        public static Win32PreviewProvider Preview { get; } = new Win32PreviewProvider(ThreadPool);
        public static Win32ThumbnailProvider Thumbnail { get; } = new Win32ThumbnailProvider(ThreadPool);
        public static Win32FilePropertyProvider Property { get; } = new Win32FilePropertyProvider(ThreadPool);
        public static Win32FileLinkResolver FileLinkResolver { get; } = new Win32FileLinkResolver(ThreadPool);
        public static Win32FileOpener FileOpener { get; } = new Win32FileOpener(ThreadPool);
        public static Win32FileProperties FileProperties { get; } = new Win32FileProperties(ThreadPool);
    }
}
