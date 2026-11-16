using System;

namespace Brows;

internal sealed class SftpStreamGui : EntryStreamGui {
    private const uint SizeFlag = 0x1;
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

    private long PreviewLength {
        get {
            var entryIsLink = Entry.Kind is FileProtocolEntryKind.FileLink or FileProtocolEntryKind.DirectoryLink;
            if (entryIsLink) {
                return 0;
            }
            var hasSize = (Entry.Info.Flags & SizeFlag) != 0;
            if (!hasSize) {
                return 0;
            }
            if (Entry.Info.Size > long.MaxValue) {
                return long.MaxValue;
            }
            return (long)Entry.Info.Size;
        }
    }

    public sealed override IEntryStreamSource Source => new FileProtocolPreviewStreamSource<SftpEntry>(
        Entry,
        Entry.RelativePath,
        PreviewLength,
        PreviewReadLimit,
        static (entry, token) => entry.Client.OpenRead(entry.RemotePath, token));

    public sealed override IEntryStreamGuiOptions Options => Entry.Provider.StreamOptions;

    public sealed override bool ForceText =>
        Entry.Kind == FileProtocolEntryKind.File &&
        PreviewOptions.Text.Extensions.Contains(Entry.Extension) &&
        !PreviewOptions.Image.Extensions.Contains(Entry.Extension);

    public sealed override bool ForceImage =>
        Entry.Kind == FileProtocolEntryKind.File &&
        PreviewOptions.Image.Extensions.Contains(Entry.Extension);

    public SftpEntry Entry { get; }

    public SftpStreamGui(SftpEntry entry) {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
    }
}
