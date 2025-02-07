using System.Collections.Generic;

namespace Brows.Url.Ftp.ListingItemParsers {
    internal sealed class MvsPds2ListingParser : FtpListingItemParser {
        private static FtpListingInfoBuilder Build(FtpListingItem line, FtpListingContext context) {
            int index = 0;
            FtpListingToken token;
            if (null == (token = line.GetToken(index))) {
                return null;
            }

            var info = new FtpListingInfoBuilder();
            info.Name = token.ToString();

            info.Flags = 0;
            info.OwnerGroup = "";
            info.Permissions = info.OwnerGroup;
            info.Size = -1;

            if (null == (token = line.GetToken(++index))) {
                return info;
            }

            info.Size = token.Base16;
            if (info.Size == -1) {
                return null;
            }

            // Unused hexadecimal token
            if (null == (token = line.GetToken(++index))) {
                return null;
            }
            if (!token.IsBase16) {
                return null;
            }

            // Unused numeric token
            if (null == (token = line.GetToken(++index))) {
                return null;
            }
            if (!token.IsBase10) {
                return null;
            }

            int start = ++index;
            while (null != (token = line.GetToken(index))) {
                ++index;
            }
            if (index - start < 2) {
                return null;
            }
            --index;

            if (null == (token = line.GetToken(index))) {
                return null;
            }
            if (!token.IsBase10 && token.ToString() != "ANY") {
                return null;
            }

            if (null == (token = line.GetToken(index - 1))) {
                return null;
            }
            if (!token.IsBase10 && token.ToString() != "ANY") {
                return null;
            }

            for (int i = start; i < index - 1; ++i) {
                if (null == (token = line.GetToken(i))) {
                    return null;
                }
                int len = token.Length;
                for (int j = 0; j < len; ++j) {
                    if (token[j] < 'A' || token[j] > 'Z') {
                        return null;
                    }
                }
            }

            return info;
        }

        protected sealed override FtpListingInfoBuilder Build(IReadOnlyList<FtpListingItem> items, FtpListingContext context) {
            return items?.Count == 1 ? Build(items[0], context) : null;
        }
    }
}
