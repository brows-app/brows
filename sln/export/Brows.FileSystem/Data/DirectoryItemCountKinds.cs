using System;

namespace Brows.Data {
    [Flags]
    internal enum DirectoryItemCountKinds {
        Any = 0,
        Directory = 1,
        File = 2
    }
}
