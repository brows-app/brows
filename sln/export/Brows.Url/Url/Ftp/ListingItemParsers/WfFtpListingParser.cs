using System.Collections.Generic;
using System.Linq;

namespace Brows.Url.Ftp.ListingItemParsers {
    internal sealed class WfFtpListingParser : FtpListingItemParser {
        private static FtpListingInfoBuilder Build(FtpListingItem line, FtpListingContext context) {
            int index = 0;
            FtpListingToken token;

            // Get filename
            if (null == (token = line.GetToken(index++)))
                return null;

            var info = new FtpListingInfoBuilder();
            info.Name = token.ToString();

            // Get filesize
            if (null == (token = line.GetToken(index++)))
                return null;

            if (!token.IsBase10)
                return null;

            info.Size = token.Base10;

            info.Flags = 0;

            // Parse date
            if (null == (token = line.GetToken(index++)))
                return null;

            if (null == (info.Time = ParseShortDate(token)))
                return null;

            // Unused token
            if (null == (token = line.GetToken(index++)))
                return null;

            if (token.ToString().Last() != '.')
                return null;

            // Parse time
            if (null == (token = line.GetToken(index++, true)))
                return null;

            if (!ParseTime(token, info))
                return null;

            info.OwnerGroup = "";
            info.Permissions = info.OwnerGroup;
            info.Time += context.TimeZoneOffset;

            return info;
        }

        protected sealed override FtpListingInfoBuilder Build(IReadOnlyList<FtpListingItem> items, FtpListingContext context) {
            return items?.Count == 1 ? Build(items[0], context) : null;
        }
    }
}
