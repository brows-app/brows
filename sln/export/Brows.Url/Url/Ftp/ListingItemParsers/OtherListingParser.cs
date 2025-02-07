using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Url.Ftp.ListingItemParsers {
    internal sealed class OtherListingParser : FtpListingItemParser {
        private static FtpListingInfoBuilder Build(FtpListingItem line, FtpListingContext context) {
            int index = 0;
            FtpListingToken firstToken;

            if (null == (firstToken = line.GetToken(index))) {
                return null;
            }

            if (!firstToken.IsBase10) {
                return null;
            }

            // Possible formats: Numerical unix, VShell or OS/2

            FtpListingToken token;
            if (null == (token = line.GetToken(++index))) {
                return null;
            }

            var info = new FtpListingInfoBuilder();
            info.Flags = 0;

            // If token is a number, than it's the numerical Unix style format,
            // else it's the VShell, OS/2 or nortel.VxWorks format
            if (token.IsBase10) {
                if (firstToken.Length >= 2 && firstToken[1] == '4') {
                    info.Flags |= FtpListingInfoFlags.Directory;
                }

                var ownerGroup = token.ToString();

                if (null == (token = line.GetToken(++index))) {
                    return null;
                }

                ownerGroup += " " + token.ToString();

                // Get size
                if (null == (token = line.GetToken(++index))) {
                    return null;
                }

                if (!token.IsBase10) {
                    return null;
                }

                info.Size = token.Base10;

                // Get date/time
                if (null == (token = line.GetToken(++index))) {
                    return null;
                }

                long number = token.Base10 ?? -1;
                if (number < 0) {
                    return null;
                }
                info.Time = Time(number);

                // Get filename
                if (null == (token = line.GetToken(++index, true))) {
                    return null;
                }

                info.Name = token.ToString();
                info.Target = "";

                info.Permissions = firstToken.ToString();
                info.OwnerGroup = ownerGroup;
            }
            else {
                // Possible conflict with multiline VMS listings
                if (context.MaybeMultilineVms) {
                    return null;
                }

                // VShell, OS/2 or nortel.VxWorks style format
                info.Size = firstToken.Base10;

                // Get date
                var dateMonth = token.ToString();
                int? month = 0;
                if (null == (month = GetMonthFromName(dateMonth))) {
                    // OS/2 or nortel.VxWorks
                    int skippedCount = 0;
                    do {
                        if (token.ToString() == "DIR") {
                            info.Flags |= FtpListingInfoFlags.Directory;
                        }

                        else if (token.Find("-/.") != -1) {
                            break;
                        }

                        ++skippedCount;

                        if (null == (token = line.GetToken(++index))) {
                            return null;
                        }
                    } while (true);

                    if (null == (info.Time = ParseShortDate(token))) {
                        return null;
                    }

                    // Get time
                    if (null == (token = line.GetToken(++index))) {
                        return null;
                    }

                    if (!ParseTime(token, info)) {
                        return null;
                    }

                    // Get filename
                    if (null == (token = line.GetToken(++index, true))) {
                        return null;
                    }

                    info.Name = token.ToString();
                    if (info.Name.Length >= 5) {
                        var type = info.Name.Substring(info.Name.Length - 5).ToLowerInvariant();
                        if (0 == skippedCount && type == "<dir>") {
                            info.Flags |= FtpListingInfoFlags.Directory;
                            info.Name = info.Name.Substring(0, info.Name.Length - 5);
                            while (!string.IsNullOrEmpty(info.Name) && info.Name.Last() == ' ') {
                                info.Name = info.Name.Substring(0, info.Name.Length - 1);
                            }
                        }
                    }
                }
                else {
                    // Get day
                    if (null == (token = line.GetToken(++index))) {
                        return null;
                    }

                    if (!token.IsBase10 && !token.IsBase10OnLeft) {
                        return null;
                    }

                    long day = token.Base10 ?? -1;
                    if (day < 0 || day > 31) {
                        return null;
                    }

                    // Get Year
                    if (null == (token = line.GetToken(++index))) {
                        return null;
                    }

                    if (!token.IsBase10) {
                        return null;
                    }

                    long year = token.Base10.Value;
                    if (year < 50) {
                        year += 2000;
                    }
                    else if (year < 1000) {
                        year += 1900;
                    }

                    if (!(year > 0 && month > 0 && day > 0)) {
                        return null;
                    }
                    info.Time = new DateTime((int)year, month.Value, (int)day, 0, 0, 0, DateTimeKind.Utc);

                    // Get time
                    if (null == (token = line.GetToken(++index))) {
                        return null;
                    }

                    if (!ParseTime(token, info)) {
                        return null;
                    }

                    // Get filename
                    if (null == (token = line.GetToken(++index, true))) {
                        return null;
                    }

                    info.Name = token.ToString();
                    var chr = token[token.Length - 1];
                    if (chr == '/' || chr == '\\') {
                        info.Flags |= FtpListingInfoFlags.Directory;
                        info.Name = info.Name.Substring(0, info.Name.Length - 1);
                    }
                }
                info.Target = "";
                info.OwnerGroup = "";
                info.Permissions = info.OwnerGroup;
                info.Time += context.TimeZoneOffset;
            }

            return info;
        }

        protected sealed override FtpListingInfoBuilder Build(IReadOnlyList<FtpListingItem> items, FtpListingContext context) {
            return items?.Count == 1 ? Build(items[0], context) : null;
        }
    }
}
