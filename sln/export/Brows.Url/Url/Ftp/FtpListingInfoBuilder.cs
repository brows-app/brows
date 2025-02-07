using System;

namespace Brows.Url.Ftp {
    internal sealed class FtpListingInfoBuilder {
        public string Name { get; set; }
        public long? Size { get; set; }
        public string Permissions { get; set; }
        public string OwnerGroup { get; set; }
        public string Target { get; set; }
        public DateTime? Time { get; set; }
        public FtpListingInfoFlags Flags { get; set; }

        public FtpListingInfo Build(string parser) {
            return new FtpListingInfo(Name, Size, Permissions, OwnerGroup, Target, Time, Flags, parser);
        }
    }
}
