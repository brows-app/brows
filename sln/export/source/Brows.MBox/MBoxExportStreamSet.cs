using System.Collections.Generic;
using System.Linq;

namespace Brows;

/*
 * The files that export the selected messages of a mailbox, one file per message. A provider
 * that can copy streams receives them, so the messages can be exported to any such provider.
 */
internal sealed class MBoxExportStreamSet : EntryStreamSet {
    private IReadOnlyList<MBoxExportStreamSource> Sources { get; }

    protected sealed override IEnumerable<IEntryStreamSource> StreamSource() {
        return Sources;
    }

    internal MBoxExportStreamSet(IEnumerable<MBoxEntry> entries, bool archive) {
        ArgumentNullException.ThrowIfNull(entries);
        Sources = entries
            .Where(entry => entry is not null)
            .Select(entry => new MBoxExportStreamSource(entry, archive))
            .ToList();
    }
}
