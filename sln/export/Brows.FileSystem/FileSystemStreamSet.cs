using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    internal sealed class FileSystemStreamSet : EntryStreamSet {
        protected sealed override IEnumerable<IEntryStreamSource> StreamSource() {
            return Collection;
        }

        protected sealed override IEnumerable<string> FileSource() {
            return Collection.Select(s => s.SourceFile);
        }

        public IEnumerable<FileSystemStreamSource> Collection { get; }

        public FileSystemStreamSet(IEnumerable<FileSystemStreamSource> collection) {
            Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        }
    }
}
