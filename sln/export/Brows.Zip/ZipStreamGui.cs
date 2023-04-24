using System;

namespace Brows {
    internal sealed class ZipStreamGui : EntryStreamGui {
        public sealed override IEntryStreamSource Source =>
             new ZipStreamSource(Entry);

        public sealed override IEntryStreamGuiOptions Options =>
            Entry.Provider.Config.Stream;

        public ZipEntry Entry { get; }

        public ZipStreamGui(ZipEntry entry) {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        }
    }
}
