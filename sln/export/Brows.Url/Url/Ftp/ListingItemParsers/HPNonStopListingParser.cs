using System;
using System.Collections.Generic;

namespace Brows.Url.Ftp.ListingItemParsers {
    internal sealed class HPNonStopListingParser : FtpListingItemParser {
        private static FtpListingInfoBuilder Build(FtpListingItem line, FtpListingContext context) {
            ArgumentNullException.ThrowIfNull(line);
            ArgumentNullException.ThrowIfNull(context);

            int index = 0;
            FtpListingToken token;

            // Get name
            if (null == (token = line.GetToken(index)))
                return null;

            var info = new FtpListingInfoBuilder();
            info.Name = token.ToString();

            // File code, numeric, unsuded
            if (null == (token = line.GetToken(++index)))
                return null;
            if (!token.IsBase10)
                return null;

            // Size
            if (null == (token = line.GetToken(++index)))
                return null;
            if (!token.IsBase10)
                return null;

            info.Size = token.Base10;

            info.Flags = 0;

            // Date
            if (null == (token = line.GetToken(++index)))
                return null;
            if (null == (info.Time = ParseShortDate(token, false)))
                return null;

            // Time
            if (null == (token = line.GetToken(++index)))
                return null;
            if (!ParseTime(token, info))
                return null;

            // Owner
            if (null == (token = line.GetToken(++index)))
                return null;
            var ownerGroup = token.ToString();

            if (token[token.Length - 1] == ',') {
                // Owner, part 2
                if (null == (token = line.GetToken(++index)))
                    return null;
                ownerGroup += " " + token.ToString();
            }

            // Permissions
            FtpListingToken permToken;
            if (null == (permToken = line.GetToken(++index)))
                return null;

            // Nothing
            if (null != (token = line.GetToken(++index)))
                return null;

            info.Permissions = permToken.ToString();
            info.OwnerGroup = ownerGroup;

            return info;
        }

        protected sealed override FtpListingInfoBuilder Build(IReadOnlyList<FtpListingItem> items, FtpListingContext context) {
            return items?.Count == 1 ? Build(items[0], context) : null;
        }
    }
}
