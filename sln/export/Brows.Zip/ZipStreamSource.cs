using System.IO;

namespace Brows {
    internal sealed class ZipStreamSource : ZipArchivePath.StreamSource<ZipEntry> {
        public sealed override string RelativePath =>
            Entry.Info.Name.Normalized
                .Substring(Entry.Provider.Zip.RelativePath.Normalized.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        public ZipStreamSource(ZipEntry entry) : base(entry, entry?.Info) {
        }

        public override string ToString() {
            return Entry.ID;
        }
    }
}
