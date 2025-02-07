using System;
using System.Collections.Generic;

namespace Brows.Url.Ftp {
    internal abstract class FtpListingItemParser {
        private static FtpListingMonthMap MonthLookup => _MonthLookup ??= new();
        private static FtpListingMonthMap _MonthLookup;

        protected static int? GetMonthFromName(string name) {
            return MonthLookup.Find(name);
        }

        protected static long? ParseComplexFileSize(FtpListingToken token, int blocksize = -1) {
            long size;
            if (token.IsBase10) {
                size = token.Base10.Value;
                if (blocksize != -1) {
                    size *= blocksize;
                }

                return size;
            }

            int len = token.Length;

            var last = token[len - 1];
            if (last == 'B' || last == 'b') {
                if (len == 1) {
                    return null;
                }

                var c = token[--len - 1];
                if (c < '0' || c > '9') {
                    --len;
                    last = c;
                }
                else {
                    last = '\0';
                }
            }
            else if (last >= '0' && last <= '9') {
                last = '\0';
            }
            else {
                if (--len == 0) {
                    return null;
                }
            }

            size = 0;

            int dot = -1;
            for (int i = 0; i < len; ++i) {
                var c = token[i];
                if (c >= '0' && c <= '9') {
                    size *= 10;
                    size += c - '0';
                }
                else if (c == '.') {
                    if (dot != -1) {
                        return null;
                    }
                    dot = len - i - 1;
                }
                else {
                    return null;
                }
            }
            switch (last) {
                case 'k':
                case 'K':
                    size *= 1024;
                    break;
                case 'm':
                case 'M':
                    size *= 1024 * 1024;
                    break;
                case 'g':
                case 'G':
                    size *= 1024 * 1024 * 1024;
                    break;
                case 't':
                case 'T':
                    size *= 1024 * 1024;
                    size *= 1024 * 1024;
                    break;
                case 'b':
                case 'B':
                    break;
                case '\0':
                    if (blocksize != -1) {
                        size *= blocksize;
                    }
                    break;
                default:
                    return null;
            }
            while (dot-- > 0) {
                size /= 10;
            }

            return size;
        }

        protected static DateTime Time(long seconds) {
            return new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) + TimeSpan.FromSeconds(seconds);
        }

        protected static bool ParseTime(FtpListingToken token, FtpListingInfoBuilder info) {
            if (info.Time == null) {
                return false;
            }
            int pos = token.Find(':');
            if (pos < 1 || pos >= token.Length - 1)
                return false;

            long hour = token.Chunk(0, pos).Base10 ?? -1;
            if (hour < 0 || hour > 24)
                return false;

            // See if we got seconds
            int pos2 = token.Find(':', pos + 1);
            int len;
            if (pos2 == -1)
                len = -1;
            else
                len = pos2 - pos - 1;

            if (0 == len)
                return false;

            long minute = token.Chunk(pos + 1, len).Base10 ?? -1;
            if (minute < 0 || minute > 59)
                return false;

            long seconds = -1;
            if (pos2 != -1) {
                // Parse seconds
                seconds = token.Chunk(pos2 + 1, -1).Base10 ?? -1;
                if (seconds < 0 || seconds > 60)
                    return false;
            }

            // Convert to 24h format
            if (!token.IsBase10OnRight) {
                if (token[token.Length - 2] == 'P') {
                    if (hour < 12)
                        hour += 12;
                }
                else if (hour == 12)
                    hour = 0;
            }

            info.Time = new DateTime(info.Time.Value.Year, info.Time.Value.Month, info.Time.Value.Day, hour >= 0 ? (int)hour : 0, minute >= 0 ? (int)minute : 0, seconds >= 0 ? (int)seconds : 0, DateTimeKind.Utc);
            return true;
        }

        protected static DateTime? ParseShortDate(FtpListingToken token, bool saneFieldOrder = false) {
            if (token.Length < 1) {
                return null;
            }

            bool gotYear = false;
            bool gotMonth = false;
            bool gotDay = false;
            bool gotMonthName = false;

            long year = 0;
            long month = 0;
            long day = 0;
            long value;

            int pos = token.Find("-./");
            if (pos < 1) {
                return null;
            }

            if (!token.Chunk(0, pos).IsBase10) {
                // Seems to be monthname-dd-yy

                // Check month name
                var dateMonth = token.ToString().Substring(0, pos);
                var m = GetMonthFromName(dateMonth);
                if (m == null) {
                    return null;
                }
                month = m.Value;
                gotMonth = true;
                gotMonthName = true;
            }
            else if (pos == 4) {
                // Seems to be yyyy-mm-dd
                year = token.Chunk(0, pos).Base10 ?? -1;
                if (year < 1900 || year > 3000) {
                    return null;
                }
                gotYear = true;
            }
            else if (pos <= 2) {
                value = token.Chunk(0, pos).Base10 ?? -1;
                if (token[pos] == '.') {
                    // Maybe dd.mm.yyyy
                    if (value < 1 || value > 31) {
                        return null;
                    }
                    day = value;
                    gotDay = true;
                }
                else {
                    if (saneFieldOrder) {
                        year = value;
                        if (year < 50) {
                            year += 2000;
                        }
                        else {
                            year += 1900;
                        }
                        gotYear = true;
                    }
                    else {
                        // Detect mm-dd-yyyy or mm/dd/yyyy and
                        // dd-mm-yyyy or dd/mm/yyyy
                        if (value < 1) {
                            return null;
                        }
                        if (value > 12) {
                            if (value > 31) {
                                return null;
                            }

                            day = value;
                            gotDay = true;
                        }
                        else {
                            month = value;
                            gotMonth = true;
                        }
                    }
                }
            }
            else {
                return null;
            }

            int pos2 = token.Find("-./", pos + 1);
            if (pos2 == -1 || pos2 - pos == 1) {
                return null;
            }
            if (pos2 == token.Length - 1) {
                return null;
            }

            // If we already got the month and the second field is not numeric,
            // change old month into day and use new token as month
            if (!token.Chunk(pos + 1, pos2 - pos - 1).IsBase10 && gotMonth) {
                if (gotMonthName) {
                    return null;
                }

                if (gotDay) {
                    return null;
                }

                gotDay = true;
                gotMonth = false;
                day = month;
            }

            if (gotYear || gotDay) {
                // Month field in yyyy-mm-dd or dd-mm-yyyy
                // Check month name
                var dateMonth = token.ToString().Substring(pos + 1, pos2 - pos - 1);
                var m = GetMonthFromName(dateMonth);
                if (m == null) {
                    return null;
                }
                month = m.Value;
                gotMonth = true;
            }
            else {
                value = token.Chunk(pos + 1, pos2 - pos - 1).Base10 ?? -1;
                // Day field in mm-dd-yyyy
                if (value < 1 || value > 31) {
                    return null;
                }
                day = value;
                gotDay = true;
            }

            value = token.Chunk(pos2 + 1, token.Length - pos2 - 1).Base10 ?? -1;
            if (gotYear) {
                // Day field in yyy-mm-dd
                if (value <= 0 || value > 31) {
                    return null;
                }
                day = value;
                gotDay = true;
            }
            else {
                if (value < 0 || value > 9999) {
                    return null;
                }

                if (value < 50) {
                    value += 2000;
                }
                else if (value < 1000) {
                    value += 1900;
                }
                year = value;

                gotYear = true;
            }

            if (!gotYear || !gotMonth || !gotDay) {
                return null;
            }

            return new DateTime(new DateOnly((int)year, (int)month, (int)day), default, DateTimeKind.Utc);
        }

        protected abstract FtpListingInfoBuilder Build(IReadOnlyList<FtpListingItem> items, FtpListingContext context);

        public string Name => _Name ??= GetType().Name[..^"ListingParser".Length].ToUpperInvariant();
        private string _Name;

        public FtpListingInfo Parse(IReadOnlyList<FtpListingItem> items, FtpListingContext context) {
            return Build(items, context)?.Build(Name);
        }

        public sealed override string ToString() {
            return Name;
        }
    }
}
