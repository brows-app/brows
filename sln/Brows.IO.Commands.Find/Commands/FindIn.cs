using System;

namespace Brows.Commands {
    [Flags]
    internal enum FindIn {
        None = 0,
        DirectoryName = 1,
        FileName = 2,
        FolderName = DirectoryName
    }
}
