using System;

namespace Brows.Commands {
    internal sealed class ClipboardPaste : ClipboardPasteIO {
        protected sealed override Type Provider => typeof(ZipProvider);
    }
}
