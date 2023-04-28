using System;

namespace Brows {
    internal sealed class ZipStreamGui : EntryStreamGui {
        public sealed override IEntryStreamSource Source =>
             new ZipStreamSource(Entry);

        public sealed override IEntryStreamGuiOptions Options =>
            Entry.Provider.Config.Stream;

        public sealed override bool ForceText =>
            Entry.Info.Kind == ZipEntryKind.File &&
            Entry.Provider.Config.Stream.Text.Extensions.Contains(Entry.Info.Extension);

        public sealed override bool ForceImage =>
            Entry.Info.Kind == ZipEntryKind.File &&
            Entry.Provider.Config.Stream.Image.Extensions.Contains(Entry.Info.Extension);

        public ZipEntry Entry { get; }

        public ZipStreamGui(ZipEntry entry) {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        }
    }
}
