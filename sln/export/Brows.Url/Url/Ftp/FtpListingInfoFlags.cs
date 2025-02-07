using System;

namespace Brows.Url.Ftp {
    [Flags]
    public enum FtpListingInfoFlags {
        File = 0,
        Directory = 1,
        Link = 2
    }
}
