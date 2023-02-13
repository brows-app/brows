using System;

namespace Brows {
    [Flags]
    internal enum DirectoryItemCountKinds {
        Any = 0,
        Directory = 1,
        File = 2
    }
}
