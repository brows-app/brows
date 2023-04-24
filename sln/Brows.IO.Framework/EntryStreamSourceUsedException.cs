using System;

namespace Brows {
    internal class EntryStreamSourceUsedException : Exception {
        public EntryStreamSource EntryStreamSource { get; }

        public EntryStreamSourceUsedException(EntryStreamSource entryStreamSource) {
            EntryStreamSource = entryStreamSource;
        }
    }
}
