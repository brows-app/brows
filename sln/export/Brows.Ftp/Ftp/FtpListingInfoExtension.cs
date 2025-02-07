using Brows.Url.Ftp;
using System;

namespace Brows.Ftp {
    internal static class FtpListingInfoExtension {
        private static FileProtocolEntryKind EntryKind(this FtpListingInfoFlags flags) {
            if (flags.HasFlag(FtpListingInfoFlags.Directory)) {
                if (flags.HasFlag(FtpListingInfoFlags.Link)) {
                    return FileProtocolEntryKind.DirectoryLink;
                }
                return FileProtocolEntryKind.Directory;
            }
            if (flags.HasFlag(FtpListingInfoFlags.File)) {
                if (flags.HasFlag(FtpListingInfoFlags.Link)) {
                    return FileProtocolEntryKind.FileLink;
                }
                return FileProtocolEntryKind.File;
            }
            throw new ArgumentOutOfRangeException(paramName: nameof(flags));
        }

        public static FileProtocolEntryKind EntryKind(this FtpListingInfo ftpListingInfo) {
            ArgumentNullException.ThrowIfNull(ftpListingInfo);
            return ftpListingInfo.Flags.EntryKind();
        }
    }
}
