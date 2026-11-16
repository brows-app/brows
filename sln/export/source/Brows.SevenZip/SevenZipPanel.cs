using Brows.Panels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows;

internal static class SevenZipPanel {
    internal static bool HasSevenZipArchive(this IPanel panel, out SevenZipArchivePath archive) {
        if (panel is null) {
            throw new ArgumentNullException(nameof(panel));
        }
        if (panel.HasProvider(out SevenZipProvider provider) is false) {
            archive = null;
            return false;
        }
        archive = provider.Archive?.ArchivePath;
        return archive is not null;
    }

    internal static bool HasSevenZipSelection(this IPanel panel, out IReadOnlyList<SevenZipEntryInfo> info) {
        if (panel is null) {
            throw new ArgumentNullException(nameof(panel));
        }
        if (panel.HasProvider(out SevenZipProvider provider) is false) {
            info = null;
            return false;
        }
        if (panel.HasSelection(out var entries) is false) {
            info = null;
            return false;
        }
        info = entries
            .OfType<SevenZipEntry>()
            .SelectMany(entry => entry.Descendants.Prepend(entry))
            .DistinctBy(entry => entry.ID)
            .Select(entry => entry.Info)
            .ToList();
        return info.Count > 0;
    }
}
