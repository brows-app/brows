using System;
using System.Collections.Generic;

namespace Brows.Url.Ftp.ListingItemParsers {
    internal sealed class EplfListingParser : FtpListingItemParser {
        private static FtpListingInfoBuilder Build(FtpListingItem line, FtpListingContext context) {
            ArgumentNullException.ThrowIfNull(line);
            ArgumentNullException.ThrowIfNull(context);

            var token = line.GetToken(0, true);
            if (token == null) {
                return null;
            }
            if (token[0] != '+')
                return null;

            int pos = token.Find('\t');
            if (pos == -1 || pos == token.Length - 1)
                return null;

            var info = new FtpListingInfoBuilder();
            info.Name = token.ToString().Substring(pos + 1);

            info.Flags = 0;
            info.Size = -1;

            var permissions = "";

            int fact = 1;
            while (fact < pos) {
                int separator = token.Find(',', fact);
                int len;
                if (separator == -1) {
                    len = pos - fact;
                }
                else {
                    len = separator - fact;
                }

                if (0 == len) {
                    ++fact;
                    continue;
                }

                var type = token[fact];

                if (type == '/') {
                    info.Flags |= FtpListingInfoFlags.Directory;
                }
                else if (type == 's') {
                    info.Size = token.Chunk(fact + 1, len - 1).Base10;
                }
                else if (type == 'm') {
                    long number = token.Chunk(fact + 1, len - 1).Base10 ?? -1;
                    if (number < 0) {
                        return null;
                    }
                    // 1970-01-01 00:00:00.000 UTC
                    info.Time = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc) + TimeSpan.FromSeconds(number);
                }
                else if (type == 'u' && len > 2 && token[fact + 1] == 'p') {
                    permissions = token.ToString().Substring(fact + 2, len - 2);
                }

                fact += len + 1;
            }

            info.Permissions = permissions;
            info.OwnerGroup = "";
            return info;
        }

        protected sealed override FtpListingInfoBuilder Build(IReadOnlyList<FtpListingItem> items, FtpListingContext context) {
            return items?.Count == 1 ? Build(items[0], context) : null;
        }
    }
}
