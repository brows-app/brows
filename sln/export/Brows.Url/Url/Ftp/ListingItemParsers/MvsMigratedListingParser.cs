using System.Collections.Generic;

namespace Brows.Url.Ftp.ListingItemParsers {
    internal sealed class MvsMigratedListingParser : FtpListingItemParser {
        private static FtpListingInfoBuilder Build(FtpListingItem line, FtpListingContext context) {
            int index = 0;
            FtpListingToken token;
            if (null == (token = line.GetToken(index)))
                return null;

            var s = token.ToString().ToLowerInvariant();
            if (s != "migrated")
                return null;

            if (null == (token = line.GetToken(++index)))
                return null;

            var info = new FtpListingInfoBuilder();
            info.Name = token.ToString();

            if (null != (token = line.GetToken(++index)))
                return null;

            info.Flags = 0;
            info.Size = -1;
            info.OwnerGroup = "";
            info.Permissions = info.OwnerGroup;

            return info;
        }

        protected sealed override FtpListingInfoBuilder Build(IReadOnlyList<FtpListingItem> items, FtpListingContext context) {
            return items?.Count == 1 ? Build(items[0], context) : null;
        }
    }
}
