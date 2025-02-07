using System;
using System.Collections.Generic;

namespace Brows.Url.Ftp.ListingItemParsers {
    internal sealed class MlsdListingParser : FtpListingItemParser {
        private static DateTime? Time(string s) {
            try {
                var year = int.Parse(s[..4]);
                var month = int.Parse(s[4..6]);
                var day = int.Parse(s[6..8]);
                var hour = s.Length >= 10 ? int.Parse(s[8..10]) : 0;
                var minute = s.Length >= 12 ? int.Parse(s[10..12]) : 0;
                var second = s.Length >= 14 ? int.Parse(s[12..14]) : 0;
                var millisecond = s.Length == 17 ? int.Parse(s[14..17]) : 0;
                return new DateTime(year, month, day, hour, minute, second, millisecond, DateTimeKind.Utc);
            }
            catch {
                return null;
            }
        }

        private static FtpListingInfoBuilder Build(FtpListingItem line, FtpListingContext context) {
            FtpListingToken token = line.GetToken(0);
            if (null == token) {
                return null;
            }

            var facts = token.View;
            if (string.IsNullOrEmpty(facts)) {
                return null;
            }

            var info = new FtpListingInfoBuilder();
            info.Flags = 0;
            info.Size = -1;
            info.Time = null;
            info.Target = null;

            string owner = "", ownername = "", group = "", groupname = "", user = "", uid = "", gid = "";
            string ownerGroup = "";
            string permissions = "";

            var start = 0;
            while (start < facts.Length) {
                var delim = facts.IndexOf(';', start);
                if (delim == -1) {
                    delim = facts.Length;
                }
                else if (delim < start + 3) {
                    return null;
                }

                var pos = facts.IndexOf('=', start);
                if (pos == -1 || pos < start + 1 || pos > delim) {
                    return null;
                }

                var factname = facts.Substring(start, pos - start).ToLowerInvariant();
                var value = facts.Substring(pos + 1, delim - pos - 1);
                if (factname == "type") {
                    var colonPos = value.IndexOf(':');
                    string valuePrefix;
                    if (colonPos == -1) {
                        valuePrefix = value.ToLowerInvariant();
                    }
                    else {
                        valuePrefix = value.Substring(0, colonPos).ToLowerInvariant();
                    }

                    if (valuePrefix == "dir" && colonPos == -1) {
                        info.Flags |= FtpListingInfoFlags.Directory;
                    }

                    else if (valuePrefix == "os.unix=slink" || valuePrefix == "os.unix=symlink") {
                        info.Flags |= FtpListingInfoFlags.Directory | FtpListingInfoFlags.Link;
                        if (colonPos != -1) {
                            var target = value.Substring(colonPos);
                            info.Target = value;
                        }
                    }

                    else if ((valuePrefix == "cdir" || valuePrefix == "pdir") && colonPos == -1) {
                        // Current and parent directory, don't parse it
                        return null; // return 2;
                    }
                }

                else if (factname == "size") {
                    info.Size = 0;

                    for (var i = 0; i < value.Length; ++i) {
                        if (value[i] < '0' || value[i] > '9') {
                            return null;
                        }
                        info.Size *= 10;
                        info.Size += value[i] - '0';
                    }
                }

                else if (factname == "modify" ||
                        info.Time == null && factname == "create") {
                    info.Time = Time(value);
                    if (info.Time == null) {
                        return null;
                    }
                }

                else if (factname == "perm") {
                    if (!string.IsNullOrEmpty(value)) {
                        if (!string.IsNullOrEmpty(permissions)) {
                            string tmp;
                            tmp = value;
                            tmp += " (";
                            tmp += permissions;
                            tmp += ")";
                            permissions = tmp;
                        }
                        else {
                            permissions = value;
                        }
                    }
                }

                else if (factname == "unix.mode") {
                    if (!string.IsNullOrEmpty(permissions)) {
                        permissions += " (";
                        permissions += value;
                        permissions += ")";
                    }
                    else {
                        permissions = value;
                    }
                }

                else if (factname == "unix.owner") {
                    owner = value;
                }

                else if (factname == "unix.ownername") {
                    ownername = value;
                }

                else if (factname == "unix.group") {
                    group = value;
                }

                else if (factname == "unix.groupname") {
                    groupname = value;
                }

                else if (factname == "unix.user") {
                    user = value;
                }

                else if (factname == "unix.uid") {
                    uid = value;
                }

                else if (factname == "unix.gid") {
                    gid = value;
                }

                start = delim + 1;
            }

            // The order of the facts is undefined, so assemble ownerGroup in correct
            // order
            if (!string.IsNullOrEmpty(ownername)) {
                ownerGroup = ownername;
            }
            else if (!string.IsNullOrEmpty(owner)) {
                ownerGroup = owner;
            }
            else if (!string.IsNullOrEmpty(user)) {
                ownerGroup = user;
            }
            else if (!string.IsNullOrEmpty(uid)) {
                ownerGroup = uid;
            }

            if (!string.IsNullOrEmpty(groupname)) {
                ownerGroup += ' ';
                ownerGroup += groupname;
            }
            else if (!string.IsNullOrEmpty(group)) {
                ownerGroup += ' ';
                ownerGroup += group;
            }
            else if (!string.IsNullOrEmpty(gid)) {
                ownerGroup += ' ';
                ownerGroup += gid;
            }

            var nameToken = line.GetEndToken(1, true);
            if (null == nameToken) {
                return null;
            }

            info.Name = nameToken.ToString();
            info.OwnerGroup = ownerGroup;
            info.Permissions = permissions;

            return info;
        }

        protected sealed override FtpListingInfoBuilder Build(IReadOnlyList<FtpListingItem> items, FtpListingContext context) {
            return items?.Count == 1 ? Build(items[0], context) : null;
        }
    }
}
