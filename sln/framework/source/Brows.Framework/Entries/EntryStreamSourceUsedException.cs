using System;

namespace Brows.Entries;

internal class EntryStreamSourceUsedException : Exception {
    public EntryStreamSource EntryStreamSource { get; }

    public EntryStreamSourceUsedException(EntryStreamSource entryStreamSource) {
        EntryStreamSource = entryStreamSource;
    }
}
