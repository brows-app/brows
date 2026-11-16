namespace Brows;

internal sealed class ZipStreamSource : ZipArchivePath.StreamSource<ZipEntry> {
    public sealed override string RelativePath => ZipStreamPath.GetRelativePath(Entry);

    public sealed override string SourceDirectory =>
        EntryInfo.Kind == ZipEntryKind.Path
            ? RelativePath
            : null;

    public ZipStreamSource(ZipEntry entry) : base(entry, entry?.Info) {
    }

    public override string ToString() {
        return Entry.ID;
    }
}
