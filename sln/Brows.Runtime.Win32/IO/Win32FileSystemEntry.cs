using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO {
    using Config;
    using Gui;
    using System;

    internal sealed class Win32FileSystemEntry : FileSystemEntry {
        private Win32FileOpener Opener =>
            _Opener ?? (
            _Opener = new Win32FileOpener());
        private Win32FileOpener _Opener;

        private Win32FileLinkResolver LinkResolver => Win32Provider.FileLinkResolver;

        protected sealed override IIconProvider IconProvider => Win32Provider.Icon;
        protected sealed override IOverlayProvider OverlayProvider => Win32Provider.Overlay;
        protected sealed override IPreviewProvider PreviewProvider => Win32Provider.Preview;
        protected sealed override IThumbnailProvider ThumbnailProvider => Win32Provider.Thumbnail;
        protected sealed override IFilePropertyProvider PropertyProvider => Kind == FileSystemEntryKind.File
            ? Win32Provider.Property
            : null;

        protected sealed override async Task<bool> Open(CancellationToken cancellationToken) {
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
                await open.Open(file, null, cancellationToken);
            }
            return true;
        }

        protected sealed override async Task<bool> Edit(CancellationToken cancellationToken) {
            var editor = default(string);
            var config = await Configure.File<EntryConfig>().Load(cancellationToken);
            if (config != null) {
                var extension = FileInfo?.Extension;
                if (extension != null) {
                    editor = config.Editor.TryGetValue(extension, out var extensionEditor)
                        ? extensionEditor
                        : null;
                }
                if (editor == null) {
                    editor = config.Editor.TryGetValue("*", out var defaultEditor)
                        ? defaultEditor
                        : null;
                }
            }
            if (editor == null) {
                return false;
            }
            var file = File;
            var open = Opener;
            await open.Open(file, editor, cancellationToken);
            return true;
        }

        public Win32FileSystemEntry(FileSystemInfo info, CancellationToken cancellationToken) : base(info, cancellationToken) {
        }

        private class EntryConfig {
            public Dictionary<string, string> Editor {
                get => _Editor ?? (_Editor = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
                set => _Editor = value;
            }
            private Dictionary<string, string> _Editor;
        }
    }
}
