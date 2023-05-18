using System;
using System.Collections.Generic;

namespace Brows.Commands {
    internal sealed class Copy : CopyIO {
        protected sealed override Type Provider => typeof(FileSystemProvider);

        protected sealed override IEnumerable<Type> Source { get; } = new[] {
            typeof(FileSystemEntry),
            typeof(FileSystemTreeNode)
        };
    }
}
