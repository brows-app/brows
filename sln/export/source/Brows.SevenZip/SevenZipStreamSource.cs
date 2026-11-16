using System.IO;

namespace Brows;

internal sealed class SevenZipStreamSource : SevenZipArchivePath.StreamSource<SevenZipEntry> {
    public sealed override string RelativePath =>
        Entry.Info.Name.Normalized
            .Substring(Entry.Provider.Archive.RelativePath.Normalized.Length)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public sealed override string SourceDirectory =>
        Entry.Info.Kind == SevenZipEntryKind.Path
            ? RelativePath
            : null;

    public SevenZipStreamSource(SevenZipEntry entry) : base(entry, entry?.Info) {
    }

    public override string ToString() {
        return Entry.ID;
    }
}
