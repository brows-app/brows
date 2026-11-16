using System;

namespace Brows;

internal sealed class TarStreamGui : EntryStreamGui {
    public sealed override IEntryStreamSource Source =>
         new TarStreamSource(Entry);

    public sealed override IEntryStreamGuiOptions Options =>
        Entry.Provider.Config.Stream;

    public sealed override bool ForceText =>
        Entry.Info.Kind == TarEntryKind.File &&
        Entry.Provider.Config.Stream.Text.Extensions.Contains(Entry.Info.Extension);

    public sealed override bool ForceImage =>
        Entry.Info.Kind == TarEntryKind.File &&
        Entry.Provider.Config.Stream.Image.Extensions.Contains(Entry.Info.Extension);

    public TarEntry Entry { get; }

    public TarStreamGui(TarEntry entry) {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
    }
}
