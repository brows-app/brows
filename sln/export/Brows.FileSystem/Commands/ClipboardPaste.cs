using System;
using System.Collections.Generic;

namespace Brows.Commands {
    internal sealed class ClipboardPaste : ClipboardPasteIO {
        protected sealed override Type Provider => typeof(FileSystemProvider);

        protected sealed override IEnumerable<Type> Source { get; } = new[] {
            typeof(IEntry),
            typeof(IEntryObservation)
        };
    }
}
