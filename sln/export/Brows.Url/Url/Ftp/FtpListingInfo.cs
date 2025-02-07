using System;

namespace Brows.Url.Ftp {
    public sealed class FtpListingInfo {
        public string Extension => _Extension ??= new Func<string>(() => {
            var i = Name.LastIndexOf('.');
            if (i >= 0) {
                return Name.Substring(i);
            }
            return "";
        })();
        private string _Extension;

        public long? Size { get; }
        public string Name { get; }
        public string Permissions { get; }
        public string OwnerGroup { get; }
        public string Target { get; }
        public DateTime? Time { get; }
        public FtpListingInfoFlags Flags { get; }
        public string Parser { get; }

        public FtpListingInfo(string name, long? size, string permissions, string ownerGroup, string target, DateTime? time, FtpListingInfoFlags flags, string parser = null) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Size = size;
            Permissions = permissions;
            OwnerGroup = ownerGroup;
            Time = time;
            Flags = flags;
            Parser = parser;
        }

        public sealed override string ToString() {
            return Name;
        }
    }
}
