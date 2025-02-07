using System;
using System.Collections.Generic;

namespace Brows.Url.Ftp.ListingItemParsers {
    internal sealed class DosListingParser : FtpListingItemParser {
        private static FtpListingInfoBuilder Build(FtpListingItem line, FtpListingContext context) {
            ArgumentNullException.ThrowIfNull(line);
            ArgumentNullException.ThrowIfNull(context);

            int index = 0;
            var info = new FtpListingInfoBuilder();

            // Get first token, has to be a valid date
            var token = line.GetToken(index);
            if (token == null) {
                return null;
            }

            info.Flags = 0;
            if (null == (info.Time = ParseShortDate(token))) {
                return null;
            }

            // Extract time
            if (null == (token = line.GetToken(++index))) {
                return null;
            }

            if (!ParseTime(token, info)) {
                return null;
            }

            // If next token is <DIR>, entry is a directory
            // else, it should be the filesize.
            if (null == (token = line.GetToken(++index)))
                return null;

            if (token.ToString() == "<DIR>") {
                info.Flags |= FtpListingInfoFlags.Directory;
                info.Size = -1;
            }

            else if (token.IsBase10 || token.IsBase10OnLeft) {
                // Convert size, filter out separators
                long size = 0;
                int len = token.Length;
                for (int i = 0; i < len; ++i) {
                    var chr = token[i];
                    if (chr == ',' || chr == '.') {
                        continue;
                    }
                    if (chr < '0' || chr > '9') {
                        return null;
                    }

                    size *= 10;
                    size += chr - '0';
                }
                info.Size = size;
            }
            else {
                return null;
            }

            // Extract filename
            if (null == (token = line.GetToken(++index, true))) {
                return null;
            }
            info.Name = token.ToString();

            info.Target = "";
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
