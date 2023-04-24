using System;

namespace Brows {
    using Data;

    internal sealed class FileSystemStreamGui : EntryStreamGui {
        public sealed override IEntryStreamSource Source =>
            new FileSystemStreamSource(Entry);

        public sealed override IEntryStreamGuiOptions Options =>
            Entry.Provider.Config.Stream;

        public object Image =>
            Entry[nameof(FileSystemInfoData.Image)];

        public FileSystemEntry Entry { get; }

        public FileSystemStreamGui(FileSystemEntry entry) {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        }
    }
}
