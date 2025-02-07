using System.Collections.Generic;

namespace Brows.Url.Ftp.ListingItemParsers {
    internal sealed class MvsListingParser : FtpListingItemParser {
        private static FtpListingInfoBuilder Build(FtpListingItem line, FtpListingContext context) {
            int index = 0;
            FtpListingToken token;

            // volume
            if (null == (token = line.GetToken(index++)))
                return null;

            // unit
            if (null == (token = line.GetToken(index++)))
                return null;

            // Referred date
            if (null == (token = line.GetToken(index++)))
                return null;

            var info = new FtpListingInfoBuilder();
            info.Flags = 0;
            if (token.ToString() != "**NONE**" && null == (info.Time = ParseShortDate(token))) {
                // Perhaps of the following type:
                // TSO004 3390 VSAM FOO.BAR
                if (token.ToString() != "VSAM")
                    return null;

                if (null == (token = line.GetToken(index++)))
                    return null;

                info.Name = token.ToString();
                if (info.Name.Contains(' '))
                    return null;

                info.Size = -1;
                info.OwnerGroup = "";
                info.Permissions = info.OwnerGroup;

                return info;
            }

            // ext
            if (null == (token = line.GetToken(index++)))
                return null;
            if (!token.IsBase10)
                return null;

            int prevLen = token.Length;

            // used
            if (null == (token = line.GetToken(index++)))
                return null;
            if (token.IsBase10 || token.ToString() == "????" || token.ToString() == "++++") {
                // recfm
                if (null == (token = line.GetToken(index++)))
                    return null;
                if (token.IsBase10)
                    return null;
            }

            else {
                if (prevLen < 6)
                    return null;
            }

            // lrecl
            if (null == (token = line.GetToken(index++)))
                return null;
            if (!token.IsBase10)
                return null;

            // blksize
            if (null == (token = line.GetToken(index++)))
                return null;
            if (!token.IsBase10)
                return null;

            // dsorg
            if (null == (token = line.GetToken(index++)))
                return null;

            if (token.ToString() == "PO" || token.ToString() == "PO-E") {
                info.Flags |= FtpListingInfoFlags.Directory;
                info.Size = -1;
            }

            else
                info.Size = 100;

            // name of dataset or sequential file
            if (null == (token = line.GetToken(index++, true)))
                return null;

            info.Name = token.ToString();

            info.OwnerGroup = "";
            info.Permissions = info.OwnerGroup;

            return info;
        }

        protected sealed override FtpListingInfoBuilder Build(IReadOnlyList<FtpListingItem> items, FtpListingContext context) {
            return items?.Count == 1 ? Build(items[0], context) : null;
        }
    }
}
