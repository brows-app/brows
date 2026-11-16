using Brows.Panels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows;

internal static class TarPanel {
    internal static bool HasTarArchive(this IPanel panel, out TarArchivePath archive) {
        if (panel is null) {
            throw new ArgumentNullException(nameof(panel));
        }
        if (panel.HasProvider(out TarProvider provider) is false) {
            archive = null;
            return false;
        }
        archive = provider.Archive?.ArchivePath;
        return archive is not null;
    }

    internal static bool HasTarSelection(this IPanel panel, out IReadOnlyList<TarEntryInfo> info) {
        if (panel is null) {
            throw new ArgumentNullException(nameof(panel));
        }
        if (panel.HasProvider(out TarProvider provider) is false) {
            info = null;
            return false;
        }
        if (panel.HasSelection(out var entries) is false) {
            info = null;
            return false;
        }
        info = entries
            .OfType<TarEntry>()
            .SelectMany(entry => entry.Descendants.Prepend(entry))
            .DistinctBy(entry => entry.ID)
            .Select(entry => entry.Info)
            .ToList();
        return info.Count > 0;
    }
}
