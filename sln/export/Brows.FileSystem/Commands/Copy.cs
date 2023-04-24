using System;

namespace Brows.Commands {
    internal sealed class Copy : CopyIO {
        protected sealed override Type Provider => typeof(FileSystemProvider);
    }
}
