using System.IO.Compression;
using System.Linq;

namespace Brows.IO.Compression;

internal static class ZipArchiveExtension {
    public static void Delete(this ZipArchive zipArchive, string entryName) {
        ArgumentNullException.ThrowIfNull(zipArchive);
        var entries = zipArchive.Entries
            .Where(entry => string.Equals(entry.FullName, entryName, StringComparison.Ordinal))
            .ToArray();
        foreach (var entry in entries) {
            entry.Delete();
        }
    }
}
