using System;

namespace Brows.Url.Ftp {
    internal sealed class FtpListingContext {
        public TimeSpan TimeZoneOffset { get; set; }
        public bool MaybeMultilineVms { get; set; }
    }
}
