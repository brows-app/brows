using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    public static class ZipPanel {
        public static bool HasZipArchive(this IPanel panel, out ZipArchivePath archive) {
            if (panel is null) throw new ArgumentNullException(nameof(panel));
            if (panel.HasProvider(out ZipProvider provider) == false) {
                archive = null;
                return false;
            }
            archive = provider.Zip?.ArchivePath;
            return archive is not null;
        }

        public static bool HasZipSelection(this IPanel panel, out IReadOnlyList<ZipEntryInfo> info) {
            if (panel is null) throw new ArgumentNullException(nameof(panel));
            if (panel.HasProvider(out ZipProvider provider) == false) {
                info = null;
                return false;
            }
            if (panel.HasSelection(out var entries) == false) {
                info = null;
                return false;
            }
            info = entries
                .OfType<ZipEntry>()
                .SelectMany(z => z.Descendants.Prepend(z))
                .DistinctBy(z => z.ID)
                .Select(z => z.Info)
                .ToList();
            return info.Count is > 0;
        }
    }
}
