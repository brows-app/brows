using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows {
    internal sealed class FileStreamSet : IEntryStreamSet {
        public IEnumerable<string> Paths { get; }

        public FileStreamSet(IEnumerable<string> paths) {
            Paths = paths ?? throw new ArgumentNullException(nameof(paths));
        }

        IEntryStreamReady IEntryStreamSet.StreamSourceReady() {
            return new EntryStreamReady();
        }

        IEnumerable<IEntryStreamSource> IEntryStreamSet.StreamSource() {
            return Paths.Select(path => new FileStreamSource(path));
        }

        IEnumerable<string> IEntryStreamSet.FileSource() {
            return Paths;
        }
    }
}
