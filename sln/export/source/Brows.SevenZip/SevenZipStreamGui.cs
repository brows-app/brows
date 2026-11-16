using System;

namespace Brows;

internal sealed class SevenZipStreamGui : EntryStreamGui {
    public sealed override IEntryStreamSource Source =>
         new SevenZipStreamSource(Entry);

    public sealed override IEntryStreamGuiOptions Options =>
        Entry.Provider.Config.Stream;

    public sealed override bool ForceText =>
        Entry.Info.Kind == SevenZipEntryKind.File &&
        Entry.Provider.Config.Stream.Text.Extensions.Contains(Entry.Info.Extension);

    public sealed override bool ForceImage =>
        Entry.Info.Kind == SevenZipEntryKind.File &&
        Entry.Provider.Config.Stream.Image.Extensions.Contains(Entry.Info.Extension);

    public SevenZipEntry Entry { get; }

    public SevenZipStreamGui(SevenZipEntry entry) {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
    }
}
