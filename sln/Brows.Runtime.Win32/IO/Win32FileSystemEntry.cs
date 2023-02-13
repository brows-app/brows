using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO {
    using Config;
    using Gui;

    internal sealed class Win32FileSystemEntry : FileSystemEntry {
        protected sealed override IIconProvider IconProvider => Provide.Icon;
        protected sealed override IPreviewProvider PreviewProvider => Provide.Preview;
        protected sealed override IOverlayProvider OverlayProvider => Provide.Overlay;
        protected sealed override IThumbnailProvider ThumbnailProvider => Provide.Thumbnail;
        protected sealed override IFilePropertyProvider PropertyProvider => Kind == FileSystemEntryKind.File
            ? Provide.Property
            : null;

        protected sealed override async Task<bool> Open(CancellationToken cancellationToken) {
            if (Info is FileInfo) {
                var file = File;
                var rslv = Provide.FileLinkResolver;
                var link = await rslv.Resolve(file, cancellationToken);
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
                var open = Provide.FileOpener;
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
            var open = Provide.FileOpener;
            await open.Open(file, editor, cancellationToken);
            return true;
        }

        protected override async Task<bool> Detail(CancellationToken cancellationToken) {
            var file = File;
            var detail = Provide.FileProperties;
            await detail.Show(file, cancellationToken);
            return true;
        }

        public Win32Provide Provide { get; }

        public Win32FileSystemEntry(FileSystemInfo info, Win32Provide provide, CancellationToken cancellationToken) : base(info, cancellationToken) {
            Provide = provide ?? throw new ArgumentNullException(nameof(provide));
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
