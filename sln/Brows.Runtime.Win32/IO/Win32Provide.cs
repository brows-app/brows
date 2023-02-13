namespace Brows.IO {
    using Gui;

    internal class Win32Provide {
        public IIconProvider Icon { get; set; } = Win32Provider.Icon;
        public IOverlayProvider Overlay { get; set; } = Win32Provider.Overlay;
        public IPreviewProvider Preview { get; set; } = Win32Provider.Preview;
        public IThumbnailProvider Thumbnail { get; set; } = Win32Provider.Thumbnail;
        public IFilePropertyProvider Property { get; set; } = Win32Provider.Property;
        public Win32FileLinkResolver FileLinkResolver { get; set; } = Win32Provider.FileLinkResolver;
        public Win32FileOpener FileOpener { get; set; } = Win32Provider.FileOpener;
        public Win32FileProperties FileProperties { get; set; } = Win32Provider.FileProperties;
    }
}
