using System.Collections.Generic;

namespace Brows.Url.Ftp.ListingItemParsers {
    internal sealed class IbmListingParser : FtpListingItemParser {
        private static FtpListingInfoBuilder Build(FtpListingItem line, FtpListingContext context) {
            int index = 0;

            // Get owner
            var ownerGroupToken = line.GetToken(index);
            if (ownerGroupToken == null)
                return null;

            // Get size
            FtpListingToken token;
            if (null == (token = line.GetToken(++index)))
                return null;

            if (!token.IsBase10)
                return null;

            var info = new FtpListingInfoBuilder();
            info.Size = token.Base10;

            // Get date
            if (null == (token = line.GetToken(++index)))
                return null;

            info.Flags = 0;

            if (null == (info.Time = ParseShortDate(token)))
                return null;

            // Get time
            if (null == (token = line.GetToken(++index)))
                return null;

            if (!ParseTime(token, info))
                return null;

            // Get filename
            if (null == (token = line.GetToken(index + 2, true)))
                return null;

            info.Name = token.ToString();
            if (token[token.Length - 1] == '/') {
                info.Name = info.Name.Substring(0, info.Name.Length - 1);
                info.Flags |= FtpListingInfoFlags.Directory;
            }

            info.OwnerGroup = ownerGroupToken.ToString();
            info.Permissions = "";

            info.Time += context.TimeZoneOffset;

            return info;
        }

        protected sealed override FtpListingInfoBuilder Build(IReadOnlyList<FtpListingItem> items, FtpListingContext context) {
            return items?.Count == 1 ? Build(items[0], context) : null;
        }
    }
}
