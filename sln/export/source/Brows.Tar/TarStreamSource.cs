using System.IO;

namespace Brows;

internal sealed class TarStreamSource : TarArchivePath.StreamSource<TarEntry> {
    public sealed override string RelativePath =>
        Entry.Info.Name.Normalized
            .Substring(Entry.Provider.Archive.RelativePath.Normalized.Length)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public sealed override string SourceDirectory =>
        Entry.Info.Kind == TarEntryKind.Path
            ? RelativePath
            : null;

    public TarStreamSource(TarEntry entry) : base(entry, entry?.Info) {
    }

    public override string ToString() {
        return Entry.ID;
    }
}
