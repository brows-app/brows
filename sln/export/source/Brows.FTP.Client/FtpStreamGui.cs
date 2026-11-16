using System;

namespace Brows;

internal sealed class FtpStreamGui : EntryStreamGui {
    private EntryStreamGuiOptions PreviewOptions => Entry.Provider.StreamOptions;

    private long? PreviewReadLimit {
        get {
            var options = PreviewOptions;
            if (ForceText && !ForceImage) {
                return options.Text.SourceLengthMax;
            }
            if (ForceImage) {
                return options.Image.SourceLengthMax;
            }
            var textLimit = options.Text.SourceLengthMax;
            var imageLimit = options.Image.SourceLengthMax;
            if (!textLimit.HasValue || !imageLimit.HasValue) {
                return null;
            }
            return Math.Max(textLimit.Value, imageLimit.Value);
        }
    }

    private long? PreviewLength => Entry.Kind is FileProtocolEntryKind.FileLink or FileProtocolEntryKind.DirectoryLink
        ? null
        : Entry.Info.Size;

    public sealed override IEntryStreamSource Source => new FileProtocolPreviewStreamSource<FtpEntry>(
        Entry,
        Entry.RelativePath,
        PreviewLength ?? 0,
        PreviewReadLimit,
        static (entry, token) => entry.Client().Read(
            entry.Kind is FileProtocolEntryKind.FileLink or FileProtocolEntryKind.DirectoryLink
                ? null
                : entry.Info.Size,
            token));

    public sealed override IEntryStreamGuiOptions Options => Entry.Provider.StreamOptions;

    public sealed override bool ForceText =>
        Entry.Kind == FileProtocolEntryKind.File &&
        PreviewOptions.Text.Extensions.Contains(Entry.Extension) &&
        !PreviewOptions.Image.Extensions.Contains(Entry.Extension);

    public sealed override bool ForceImage =>
        Entry.Kind == FileProtocolEntryKind.File &&
        PreviewOptions.Image.Extensions.Contains(Entry.Extension);

    public FtpEntry Entry { get; }

    public FtpStreamGui(FtpEntry entry) {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
    }
}
