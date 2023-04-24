using System;

namespace Brows.Commands {
    internal sealed class ClipboardCopy : ClipboardCopyIO {
        protected sealed override Type Provider => typeof(FileSystemProvider);
    }
}
