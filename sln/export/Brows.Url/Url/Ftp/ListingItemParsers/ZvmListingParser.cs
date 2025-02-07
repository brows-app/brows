using System.Collections.Generic;

namespace Brows.Url.Ftp.ListingItemParsers {
    internal sealed class ZvmListingParser : FtpListingItemParser {
        private static FtpListingInfoBuilder Build(FtpListingItem line, FtpListingContext context) {

            int index = 0;
            FtpListingToken token;

            // Get name
            if (null == (token = line.GetToken(index)))
                return null;

            var info = new FtpListingInfoBuilder();
            info.Name = token.ToString();

            // Get filename extension
            if (null == (token = line.GetToken(++index)))
                return null;
            info.Name += "." + token.ToString();

            // File format. Unused
            if (null == (token = line.GetToken(++index)))
                return null;
            var format = token.ToString();
            if (format != "V" && format != "F")
                return null;

            // Record length
            if (null == (token = line.GetToken(++index)))
                return null;

            if (!token.IsBase10)
                return null;

            info.Size = token.Base10;

            // Number of records
            if (null == (token = line.GetToken(++index)))
                return null;

            if (!token.IsBase10)
                return null;

            info.Size *= token.Base10;

            // Unused (Block size?)
            if (null == (token = line.GetToken(++index)))
                return null;

            if (!token.IsBase10)
                return null;

            info.Flags = 0;

            // Date
            if (null == (token = line.GetToken(++index)))
                return null;

            if (null == (info.Time = ParseShortDate(token, true)))
                return null;

            // Time
            if (null == (token = line.GetToken(++index)))
                return null;

            if (!ParseTime(token, info))
                return null;

            // Owner
            FtpListingToken ownerGroupToken;
            if (null == (ownerGroupToken = line.GetToken(++index)))
                return null;

            // No further token!
            if (null != (token = line.GetToken(++index)))
                return null;

            info.OwnerGroup = ownerGroupToken.ToString();
            info.Permissions = "";
            info.Target = null;
            info.Time += context.TimeZoneOffset;

            return info;
        }

        protected sealed override FtpListingInfoBuilder Build(IReadOnlyList<FtpListingItem> items, FtpListingContext context) {
            return items?.Count == 1 ? Build(items[0], context) : null;
        }
    }
}
