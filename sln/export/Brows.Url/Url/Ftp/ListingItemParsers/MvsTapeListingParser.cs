using System.Collections.Generic;

namespace Brows.Url.Ftp.ListingItemParsers {
    internal sealed class MvsTapeListingParser : FtpListingItemParser {
        private static FtpListingInfoBuilder Build(FtpListingItem line, FtpListingContext context) {
            int index = 0;
            FtpListingToken token;

            // volume
            if (null == (token = line.GetToken(index++))) {
                return null;
            }

            // unit
            if (null == (token = line.GetToken(index++))) {
                return null;
            }

            var s = token.ToString().ToLowerInvariant();
            if (s != "tape") {
                return null;
            }

            // dsname
            if (null == (token = line.GetToken(index++))) {
                return null;
            }

            var info = new FtpListingInfoBuilder();
            info.Name = token.ToString();
            info.Flags = 0;
            info.OwnerGroup = "";
            info.Permissions = "";
            info.Size = -1;

            if (null != (token = line.GetToken(index++))) {
                return null;
            }

            return info;
        }

        protected sealed override FtpListingInfoBuilder Build(IReadOnlyList<FtpListingItem> items, FtpListingContext context) {
            return items?.Count == 1 ? Build(items[0], context) : null;
        }
    }
}
