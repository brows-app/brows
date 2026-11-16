using Brows.Panels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows;

/// <summary>
/// Holds the helpers that read the selection of a panel that shows a mailbox.
/// </summary>
internal static class MBoxPanel {
    /// <summary>
    /// Returns the messages of the active generation of the mailbox that the panel shows.
    /// </summary>
    /// <param name="panel">
    /// The panel to read.
    /// </param>
    /// <param name="entries">
    /// The messages of the active generation of the mailbox.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the panel shows a mailbox, and the selection holds a message of
    /// its active generation.
    /// </returns>
    public static bool HasMBoxSelection(this IPanel panel, out IReadOnlyList<MBoxEntry> entries) {
        if (panel is null) {
            throw new ArgumentNullException(nameof(panel));
        }
        if (panel.HasProvider(out MBoxProvider provider) == false) {
            entries = null;
            return false;
        }
        if (panel.HasSelection<MBoxEntry>(out var selected) == false) {
            entries = null;
            return false;
        }
        var generation = provider.ActiveGenerationId;
        entries = selected
            .Where(entry => string.Equals(entry.GenerationId, generation, StringComparison.Ordinal))
            .ToList();
        return entries.Count > 0;
    }
}
