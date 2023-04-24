using System;
using System.Collections.Generic;

namespace Brows {
    public abstract class EntryStreamSet : IEntryStreamSet {
        protected abstract IEnumerable<IEntryStreamSource> StreamSource();

        protected virtual IEnumerable<string> FileSource() {
            return Array.Empty<string>();
        }

        protected virtual IEntryStreamReady StreamSourceReady() {
            return new EntryStreamReady();
        }

        IEntryStreamReady IEntryStreamSet.StreamSourceReady() => StreamSourceReady();
        IEnumerable<IEntryStreamSource> IEntryStreamSet.StreamSource() => StreamSource();
        IEnumerable<string> IEntryStreamSet.FileSource() => FileSource();

        public static IEntryStreamSet FromFiles(IEnumerable<string> paths) {
            return new FileStreamSet(paths);
        }
    }
}
