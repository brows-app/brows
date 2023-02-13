using System;

namespace Brows {
    [Flags]
    public enum EntryRefresh {
        None = 0,
        Data = 1 << 0,
        Icon = 1 << 1,
        Thumbnail = 1 << 2,
        Preview = 1 << 3,
        Overlay = 1 << 4,
        All =
            Data |
            Icon |
            Thumbnail |
            Preview |
            Overlay
    }
}
