using System.Collections.Generic;

namespace Brows.Url.Ftp.ListingItemParsers {
    internal sealed class MvsPdsListingParser : FtpListingItemParser {
        private static FtpListingInfoBuilder Build(FtpListingItem line, FtpListingContext context) {
            int index = 0;
            FtpListingToken token;

            // pds member name
            if (null == (token = line.GetToken(index++)))
                return null;

            var info = new FtpListingInfoBuilder();
            info.Name = token.ToString();

            // vv.mm
            if (null == (token = line.GetToken(index++)))
                return null;

            info.Flags = 0;

            // creation date
            if (null == (token = line.GetToken(index++)))
                return null;
            if (null == (info.Time = ParseShortDate(token)))
                return null;

            // modification date
            if (null == (token = line.GetToken(index++)))
                return null;
            if (null == (info.Time = ParseShortDate(token)))
                return null;

            // modification time
            if (null == (token = line.GetToken(index++)))
                return null;
            if (!ParseTime(token, info))
                return null;

            // size
            if (null == (token = line.GetToken(index++)))
                return null;
            if (!token.IsBase10)
                return null;
            info.Size = token.Base10;

            // init
            if (null == (token = line.GetToken(index++)))
                return null;
            if (!token.IsBase10)
                return null;

            // mod
            if (null == (token = line.GetToken(index++)))
                return null;
            if (!token.IsBase10)
                return null;

            // id
            if (null == (token = line.GetToken(index++, true)))
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
