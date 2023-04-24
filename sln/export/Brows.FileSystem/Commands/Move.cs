using System;

namespace Brows.Commands {
    internal sealed class Move : MoveIO {
        protected sealed override Type Provider => typeof(FileSystemProvider);
    }
}
