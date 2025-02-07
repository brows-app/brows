using System.Collections.Generic;

namespace Brows.Url.Ftp.ListingItemParsers {
    internal sealed class OS9ListingParser : FtpListingItemParser {
        private static FtpListingInfoBuilder Build(FtpListingItem line, FtpListingContext context) {
            int index = 0;

            // Get owner
            var ownerGroupToken = line.GetToken(index++);
            if (ownerGroupToken == null) {
                return null;
            }

            // Make sure it's number.number
            int pos = ownerGroupToken.Find('.');
            if (pos == -1 || 0 == pos || pos == ownerGroupToken.Length - 1)
                return null;

            if (!ownerGroupToken.Chunk(0, pos).IsBase10)
                return null;

            if (!ownerGroupToken.Chunk(pos + 1, ownerGroupToken.Length - pos - 1).IsBase10)
                return null;

            var info = new FtpListingInfoBuilder();
            info.Flags = 0;

            // Get date
            FtpListingToken token;
            if (null == (token = line.GetToken(index++)))
                return null;

            if (null == (info.Time = ParseShortDate(token, true)))
                return null;

            // Unused token
            if (null == (token = line.GetToken(index++)))
                return null;

            // Get perms
            var permToken = line.GetToken(index++);
            if (permToken == null)
                return null;

            if (permToken[0] == 'd')
                info.Flags |= FtpListingInfoFlags.Directory;

            // Unused token
            if (null == (token = line.GetToken(index++)))
                return null;

            // Get Size
            if (null == (token = line.GetToken(index++)))
                return null;

            if (!token.IsBase10)
                return null;

            info.Size = token.Base10;

            // Filename
            if (null == (token = line.GetToken(index++, true)))
                return null;

            info.Name = token.ToString();
            info.OwnerGroup = ownerGroupToken.ToString();
            info.Permissions = permToken.ToString();

            return info;
        }

        protected sealed override FtpListingInfoBuilder Build(IReadOnlyList<FtpListingItem> items, FtpListingContext context) {
            return items?.Count == 1 ? Build(items[0], context) : null;
        }
    }
}
