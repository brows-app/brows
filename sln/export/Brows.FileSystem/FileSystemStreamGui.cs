using Brows.Data;
using System;

namespace Brows {
    internal sealed class FileSystemStreamGui : EntryStreamGui {
        public sealed override IEntryStreamSource Source =>
            new FileSystemStreamSource(Entry);

        public sealed override IEntryStreamGuiOptions Options =>
            Entry.Provider.Config.Stream;

        public object Image =>
            Entry[nameof(FileSystemInfoData.Image)];

        public FileSystemEntry Entry { get; }

        public sealed override bool ForceText =>
            Entry.Kind == FileSystemEntryKind.File &&
            Entry.Provider.Config.Stream.Text.Extensions.Contains(Entry.Extension);

        public sealed override bool ForceImage =>
            Entry.Kind == FileSystemEntryKind.Directory || (
            Entry.Kind == FileSystemEntryKind.File &&
            Entry.Provider.Config.Stream.Image.Extensions.Contains(Entry.Extension));

        public sealed override bool ForcePreview =>
            Entry.Kind == FileSystemEntryKind.File &&
            Entry.Provider.Config.Stream.Preview.Extensions.Contains(Entry.Extension);

        public FileSystemStreamGui(FileSystemEntry entry) {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        }
    }
}
