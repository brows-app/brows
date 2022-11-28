using System;

namespace Brows {
    [Flags]
    public enum EntryRefresh {
        None = 0,
        Data = 1 << 0,
        Icon = 1 << 1,
        Thumbnail = 1 << 2,
        PreviewText = 1 << 3,
        PreviewImage = 1 << 4,
        Overlay = 1 << 5,
        All =
            Data |
            Icon |
            Thumbnail |
            PreviewText |
            PreviewImage |
            Overlay
    }
}
