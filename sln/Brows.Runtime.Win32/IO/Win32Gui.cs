namespace Brows.IO {
    using Gui;
    using Threading;

    internal class Win32Gui {
        private static readonly StaThreadPool ThreadPool = new StaThreadPool(nameof(Win32Gui));

        public static IIconProvider IconProvider { get; } = new Win32IconProvider(ThreadPool);
        public static IOverlayProvider OverlayProvider { get; } = new Win32OverlayProvider();
        public static IPreviewProvider PreviewProvider { get; } = new Win32PreviewProvider(ThreadPool);
        public static IThumbnailProvider ThumbnailProvider { get; } = new Win32ThumbnailProvider(ThreadPool);
        public static Win32FileLinkResolver FileLinkResolver { get; } = new Win32FileLinkResolver(ThreadPool);
    }
}
