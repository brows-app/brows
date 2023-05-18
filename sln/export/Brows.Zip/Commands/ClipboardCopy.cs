using System;
using System.Collections.Generic;

namespace Brows.Commands {
    internal sealed class ClipboardCopy : ClipboardCopyIO {
        protected sealed override Type Provider => typeof(ZipProvider);

        protected sealed override IEnumerable<Type> Source { get; } = new[] {
            typeof(IEntry)
        };
    }
}
