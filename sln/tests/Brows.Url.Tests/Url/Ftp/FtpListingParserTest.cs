using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Url.Ftp {
    [TestFixture]
    public sealed class FtpListingParserTest {
        private FtpListingParser Subject {
            get => _Subject ??= new();
            set => _Subject = value;
        }
        private FtpListingParser _Subject;

        private void Compare(string line, FtpListingInfo expectedInfo, Func<FtpListingInfo, object> callback) {
            async IAsyncEnumerable<string> contents() {
                yield return line;
                await Task.CompletedTask;
            }
            var actualInfo = Subject.ParseContents(contents(), default).ToBlockingEnumerable().Single();
            var actualValue = callback(actualInfo);
            var expectedValue = callback(expectedInfo);
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        private void Compare(int i, FtpListingInfo expectedInfo, Func<FtpListingInfo, object> callback) {
            static async IAsyncEnumerable<string> contents() {
                foreach (var line in TestLines) {
                    yield return line;
                    yield return "\r\n";
                    await Task.CompletedTask;
                }
            }
            var infos = Subject.ParseContents(contents(), default).ToBlockingEnumerable().ToList();
            var actualInfo = infos[i];
            var actualValue = callback(actualInfo);
            var expectedValue = callback(expectedInfo);
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }

        private static int Year(int month, int day) {
            var now = DateTime.Now;
            var tm = now;
            var cur_year = tm.Year;
            var cur_month = tm.Month;
            var cur_day = tm.Day;
            var day_of_year = (new DateTime(tm.Year, month, day) - new DateTime(tm.Year, 1, 1)).TotalDays;
            var cur_day_of_year = (new DateTime(tm.Year, tm.Month, tm.Day) - new DateTime(tm.Year, 1, 1)).TotalDays;
            if (day_of_year > cur_day_of_year + 1) {
                return cur_year - 1;
            }
            else {
                return cur_year;
            }
        }

        public static object[] TestCases = [
            new object[] {
                "dr-xr-xr-x   2 root     other        512 Apr  8  1994 01-unix-std dir", new FtpListingInfo(
                    "01-unix-std dir",
                    512,
                    "dr-xr-xr-x",
                    "root other",
                    null,
                    new DateTime(1994, 4, 8, 0, 0, 0, DateTimeKind.Utc),
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                "-rw-r--r--   1 root     other        531 3 29 03:26 02-unix-std file", new FtpListingInfo(
                    "02-unix-std file",
                    531,
                    "-rw-r--r--",
                    "root other",
                    null,
                    new DateTime(Year(3, 29), 3, 29, 3, 26, 0, DateTimeKind.Utc),
                    0),
            },
            new object[] {
                "dr-xr-xr-x   2 root                  512 Apr  8  1994 03-unix-nogroup dir", new FtpListingInfo(
                    "03-unix-nogroup dir",
                    512,
                    "dr-xr-xr-x",
                    "root",
                    null,
                    new DateTime(1994, 4, 8, 0, 0, 0, DateTimeKind.Utc),
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                "lrwxrwxrwx   1 root     other          7 Jan 25 00:17 04-unix-std link -> usr/bin", new FtpListingInfo(
                    "04-unix-std link",
                    7,
                    "lrwxrwxrwx",
                    "root other",
                    "usr/bin",
                    new DateTime(Year(1, 25), 1, 25, 0, 17, 0, DateTimeKind.Utc),
                    FtpListingInfoFlags.Directory | FtpListingInfoFlags.Link)
            },
            new object[] {
                "-rw-r--r--   1 root     other        531 09-26 2000 05-unix-date file", new FtpListingInfo(
                    "05-unix-date file",
                    531,
                    "-rw-r--r--",
                    "root other",
                    null,
                    new DateTime(2000, 9, 26, 0, 0, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "-rw-r--r--   1 root     other        531 09-26 13:45 06-unix-date file", new FtpListingInfo(
                    "06-unix-date file",
                    531,
                    "-rw-r--r--",
                    "root other",
                    null,
                    new DateTime(Year(9, 26), 9, 26, 13, 45, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "-rw-r--r--   1 root     other        531 2005-06-07 21:22 07-unix-date file", new FtpListingInfo(
                    "07-unix-date file",
                    531,
                    "-rw-r--r--",
                    "root other",
                    null,
                    new DateTime(2005, 6, 7, 21, 22, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "-rw-r--r--   1 root     other  33.5k Oct 5 21:22 08-unix-namedsize file", new FtpListingInfo(
                    "08-unix-namedsize file",
                    335 * 1024 / 10,
                    "-rw-r--r--",
                    "root other",
                    null,
                    new DateTime(Year(10, 5), 10, 5, 21, 22, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "d [R----F--] supervisor            512       Jan 16 18:53    09-netware dir", new FtpListingInfo(
                    "09-netware dir",
                    512,
                    "d [R----F--]",
                    "supervisor",
                    null,
                    new DateTime(Year(1, 16), 1, 16, 18, 53, 0, DateTimeKind.Utc),
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                "- [R----F--] rhesus             214059       Oct 20 15:27    10-netware file", new FtpListingInfo(
                    "10-netware file",
                    214059,
                    "- [R----F--]",
                    "rhesus",
                    null,
                    new DateTime(Year(10, 20), 10, 20, 15, 27, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "-------r--         326  1391972  1392298 Nov 22  1995 11-netpresenz file", new FtpListingInfo(
                    "11-netpresenz file",
                    1392298,
                    "-------r--",
                    "1391972",
                    null,
                    new DateTime(1995, 11, 22, 0, 0, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "drwxrwxr-x               folder        2 May 10  1996 12-netpresenz dir", new FtpListingInfo(
                    "12-netpresenz dir",
                    2,
                    "drwxrwxr-x",
                    "folder",
                    null,
                    new DateTime(1996, 5, 10, 0, 0, 0, DateTimeKind.Utc),
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                "-rw-r--r--   1 group domain user 531 Jan 29 03:26 13-unix-domain file", new FtpListingInfo(
                    "13-unix-domain file",
                    531,
                    "-rw-r--r--",
                    "group domain user",
                    null,
                    new DateTime(Year(1, 29), 1, 29, 3, 26, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "+i8388621.48594,m825718503,r,s280,up755\t14-eplf file", new FtpListingInfo(
                    "14-eplf file",
                    280,
                    "755",
                    "",
                    null,
                    new DateTime(1996, 3, 1, 22, 15, 3, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "+i8388621.50690,m824255907,/,\t15-eplf dir", new FtpListingInfo(
                    "15-eplf dir",
                    -1,
                    "",
                    "",
                    null,
                    new DateTime(1996, 2, 13, 23, 58, 27, DateTimeKind.Utc),
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                "04-27-00  12:09PM       <DIR>          16-dos-dateambiguous dir", new FtpListingInfo(
                    "16-dos-dateambiguous dir",
                    -1,
                    "",
                    "",
                    null,
                    new DateTime(2000, 4, 27, 12, 9, 0, DateTimeKind.Utc),
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                "04-06-00  03:47PM                  589 17-dos-dateambiguous file", new FtpListingInfo(
                    "17-dos-dateambiguous file",
                    589,
                    "",
                    "",
                    null,
                    new DateTime(2000, 4, 6, 15, 47, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "2002-09-02  18:48       <DIR>          18-dos-longyear dir", new FtpListingInfo(
                    "18-dos-longyear dir",
                    -1,
                    "",
                    "",
                    null,
                    new DateTime(2002, 9, 2, 18, 48, 0, DateTimeKind.Utc),
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                "2002-09-02  19:06                9,730 19-dos-longyear file", new FtpListingInfo(
                    "19-dos-longyear file",
                    9730,
                    "",
                    "",
                    null,
                    new DateTime(2002, 9, 2, 19, 6, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "0100644   500  101   12345    123456789       20-unix-numerical file", new FtpListingInfo(
                    "20-unix-numerical file",
                    12345,
                    "0100644",
                    "500 101",
                    null,
                    new DateTime(1973, 11, 29, 21, 33, 9, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "206876  Apr 04, 2000 21:06 21-vshell-old file", new FtpListingInfo(
                    "21-vshell-old file",
                    206876,
                    "",
                    "",
                    null,
                    new DateTime(2000, 4, 4, 21, 6, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "0  Dec 12, 2002 02:13 22-vshell-old dir/", new FtpListingInfo(
                    "22-vshell-old dir",
                    0,
                    "",
                    "",
                    null,
                    new DateTime(2002, 12, 12, 2, 13, 0, DateTimeKind.Utc),
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                "-rwxr-xr-x    1 user group        9 Oct 08, 2002 09:47 23-vshell-new file", new FtpListingInfo(
                    "23-vshell-new file",
                    9,
                    "-rwxr-xr-x",
                    "user group",
                    null,
                    new DateTime(2002, 10, 8, 9, 47, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "36611      A    04-23-103  10:57  24-os2 file", new FtpListingInfo(
                    "24-os2 file",
                    36611,
                    "",
                    "",
                    null,
                    new DateTime(2003, 4, 23, 10, 57, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                " 1123      A    07-14-99   12:37  25-os2 file", new FtpListingInfo(
                    "25-os2 file",
                    1123,
                    "",
                    "",
                    null,
                    new DateTime(1999, 7, 14, 12, 37, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "    0 DIR       02-11-103  16:15  26-os2 dir", new FtpListingInfo(
                    "26-os2 dir",
                    0,
                    "",
                    "",
                    null,
                    new DateTime(2003, 2, 11, 16, 15, 0, DateTimeKind.Utc),
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                " 1123 DIR  A    10-05-100  23:38  27-os2 dir", new FtpListingInfo(
                    "27-os2 dir",
                    1123,
                    "",
                    "",
                    null,
                    new DateTime(2000, 10, 5, 23, 38, 0, DateTimeKind.Utc),
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                "dr-xr-xr-x   2 root     other      2235 26. Juli, 20:10 28-datetest-ger dir", new FtpListingInfo(
                    "28-datetest-ger dir",
                    2235,
                    "dr-xr-xr-x",
                    "root other",
                    null,
                    new DateTime(Year(7, 26), 7, 26, 20, 10, 0, DateTimeKind.Utc),
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                "dr-xr-xr-x   2 root     other      2235 szept 26 20:10 28b-datetest-hungarian dir", new FtpListingInfo(
                    "28b-datetest-hungarian dir",
                    2235,
                    "dr-xr-xr-x",
                    "root other",
                    null,
                    new DateTime(Year(9, 26), 9, 26, 20, 10, 0, DateTimeKind.Utc),
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                "-r-xr-xr-x   2 root     other      2235 2.   Okt.  2003 29-datetest-ger file", new FtpListingInfo(
                    "29-datetest-ger file",
                    2235,
                    "-r-xr-xr-x",
                    "root other",
                    null,
                    new DateTime(2003, 10, 2, 0, 0, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "-r-xr-xr-x   2 root     other      2235 1999/10/12 17:12 30-datetest file", new FtpListingInfo(
                    "30-datetest file",
                    2235,
                    "-r-xr-xr-x",
                    "root other",
                    null,
                    new DateTime(1999, 10, 12, 17, 12, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "-r-xr-xr-x   2 root     other      2235 24-04-2003 17:12 31-datetest file", new FtpListingInfo(
                    "31-datetest file",
                    2235,
                    "-r-xr-xr-x",
                    "root other",
                    null,
                    new DateTime(2003, 4, 24, 17, 12, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "-rw-r--r--   1 root       sys           8473  4\x8c\x8e 18\x93\xfa 2003\x94\x4e 32-datatest-japanese file", new FtpListingInfo(
                    "32-datatest-japanese file",
                    8473,
                    "-rw-r--r--",
                    "root sys",
                    null,
                    new DateTime(2003, 4, 18, 0, 0, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "-rwxrwxrwx   1 root     staff          0 2003   3\xed\xef 20 33-asian date file", new FtpListingInfo(
                    "33-asian date file",
                    0,
                    "-rwxrwxrwx",
                    "root staff",
                    null,
                    new DateTime(2003, 3, 20, 0, 0, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "-r--r--r-- 1 root root 2096 8\xed 17 08:52 34-asian date file", new FtpListingInfo(
                    "34-asian date file",
                    2096,
                    "-r--r--r--",
                    "root root",
                    null,
                    new DateTime(Year(8, 17), 8, 17, 8, 52, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "-r-xr-xr-x   2 root  root  96 2004.07.15   35-dotted-date file", new FtpListingInfo(
                    "35-dotted-date file",
                    96,
                    "-r-xr-xr-x",
                    "root root",
                    null,
                    new DateTime(2004, 7, 15, 0, 0, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "36-vms-dir.DIR;1  1 19-NOV-2001 21:41 [root,root] (RWE,RWE,RE,RE)", new FtpListingInfo(
                    "36-vms-dir",
                    512,
                    "RWE,RWE,RE,RE",
                    "root,root",
                    null,
                    new DateTime(2001, 11, 19, 21, 41, 0, DateTimeKind.Utc),
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                "37-vms-file;1       155   2-JUL-2003 10:30:13.64", new FtpListingInfo(
                    "37-vms-file;1",
                    79360,
                    "",
                    "",
                    null,
                    new DateTime(2003, 7, 2, 10, 30, 13, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "38-vms-notime-file;1    2/8    7-JAN-2000    [IV2_XXX]   (RWED,RWED,RE,)", new FtpListingInfo(
                    "38-vms-notime-file;1",
                    1024,
                    "RWED,RWED,RE,",
                    "IV2_XXX",
                    null,
                    new DateTime(2000, 1, 7, 0, 0, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "39-vms-notime-file;1    6/8    15-JUI-2002    PRONAS   (RWED,RWED,RE,)", new FtpListingInfo(
                    "39-vms-notime-file;1",
                    3072,
                    "RWED,RWED,RE,",
                    "PRONAS",
                    null,
                    new DateTime(2002, 7, 15, 0, 0, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "40-vms-multiline-file;1\r\n170774/170775     24-APR-2003 08:16:15  [FTP_CLIENT,SCOT]      (RWED,RWED,RE,)", new FtpListingInfo(
                    "40-vms-multiline-file;1",
                    87436288,
                    "RWED,RWED,RE,",
                    "FTP_CLIENT,SCOT",
                    null,
                    new DateTime(2003, 4, 24, 8, 16, 15, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "41-vms-multiline-file;1\r\n10     2-JUL-2003 10:30:08.59  [FTP_CLIENT,SCOT]      (RWED,RWED,RE,)", new FtpListingInfo(
                    "41-vms-multiline-file;1",
                    5120,
                    "RWED,RWED,RE,",
                    "FTP_CLIENT,SCOT",
                    null,
                    new DateTime(2003, 7, 2, 10, 30, 8, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "42-vms-alternate-field-order-file;1   [SUMMARY]    1/3     2-AUG-2006 13:05  (RWE,RWE,RE,)", new FtpListingInfo(
                    "42-vms-alternate-field-order-file;1",
                    512,
                    "RWE,RWE,RE,",
                    "SUMMARY",
                    null,
                    new DateTime(2006, 8, 2, 13, 5, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "43-vms-alternate-field-order-file;1       17-JUN-1994 17:25:37     6308/13     (RWED,RWED,R,)", new FtpListingInfo(
                    "43-vms-alternate-field-order-file;1",
                    3229696,
                    "RWED,RWED,R,",
                    "",
                    null,
                    new DateTime(1994, 6, 17, 17, 25, 37, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "QSYS            77824 02/23/00 15:09:55 *DIR 44-ibm-as400 dir/", new FtpListingInfo(
                    "44-ibm-as400 dir",
                    77824,
                    "",
                    "QSYS",
                    null,
                    new DateTime(2000, 2, 23, 15, 9, 55, DateTimeKind.Utc),
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                "QSYS            77824 23/02/00 15:09:55 *FILE 45-ibm-as400-date file", new FtpListingInfo(
                    "45-ibm-as400-date file",
                    77824,
                    "",
                    "QSYS",
                    null,
                    new DateTime(2000, 2, 23, 15, 9, 55, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "-r-xr-xr-x longowner longgroup123456 Feb 12 17:20 46-unix-concatsize file", new FtpListingInfo(
                    "46-unix-concatsize file",
                    123456,
                    "-r-xr-xr-x",
                    "longowner longgroup",
                    null,
                    new DateTime(Year(2, 12), 2, 12, 17, 20, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "-r-xr-xr-x 2 owner group 4512 01-jun-99 47_unix_shortdatemonth file", new FtpListingInfo(
                    "47_unix_shortdatemonth file",
                    4512,
                    "-r-xr-xr-x",
                    "owner group",
                    null,
                    new DateTime(1999, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "48-nortel-wfftp-file       1014196  06/03/04  Thur.   10:20:03", new FtpListingInfo(
                    "48-nortel-wfftp-file",
                    1014196,
                    "",
                    "",
                    null,
                    new DateTime(2004, 6, 3, 10, 20, 3, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "2048    Feb-28-1998  05:23:30   49-nortel-vxworks dir <DIR>", new FtpListingInfo(
                    "49-nortel-vxworks dir",
                    2048,
                    "",
                    "",
                    null,
                    new DateTime(1998, 2, 28, 5, 23, 30, DateTimeKind.Utc),
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                "-C--E-----FTP B BCC3I1       7670  1294495 Jan 13 07:42 50-conent file", new FtpListingInfo(
                    "50-conent file",
                    1294495,
                    "-C--E-----FTP",
                    "B BCC3I1 7670",
                    null,
                    new DateTime(Year(1, 13), 1, 13, 7, 42, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "20.20 07/03/29 1026 d-ewrewr 2650 85920 51-OS-9 dir", new FtpListingInfo(
                    "51-OS-9 dir",
                    85920,
                    "d-ewrewr",
                    "20.20",
                    null,
                    new DateTime(2007, 3, 29, 0, 0, 0, DateTimeKind.Utc),
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                "drwxr-xr-x 3 user group 512 01 oct 2004 52-swapped-daymonth dir", new FtpListingInfo(
                    "52-swapped-daymonth dir",
                    512,
                    "drwxr-xr-x",
                    "user group",
                    null,
                    new DateTime(2004, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                "-r--r--r-- 0125039 12 Nov 11 2005 53-noownergroup file", new FtpListingInfo(
                    "53-noownergroup file",
                    12,
                    "-r--r--r--",
                    "",
                    null,
                    new DateTime(2005, 11, 11, 0, 0, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "drwxr-xr-x   5 root     sys          512 2005\xEB\x85\x84  1\xEC\x9B\x94  6\xEC\x9D\xBC 54-asian date year first dir", new FtpListingInfo(
                    "54-asian date year first dir",
                    512,
                    "drwxr-xr-x",
                    "root sys",
                    null,
                    new DateTime(2005, 1, 6, 0, 0, 0, DateTimeKind.Utc),
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                "QPGMR           36864 18.09.06 14:21:26 *FILE      55-AS400.FILE", new FtpListingInfo(
                    "55-AS400.FILE",
                    36864,
                    "",
                    "QPGMR",
                    null,
                    new DateTime(2006, 9, 18, 14, 21, 26, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "56-VMS-complex-size;1 2KB 23-SEP-2005 14:57:07.27", new FtpListingInfo(
                    "56-VMS-complex-size;1",
                    2048,
                    "",
                    "",
                    null,
                    new DateTime(2005, 9, 23, 14, 57, 7, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "57-HP_NonStop 101 528 6-Apr-07 14:21:18 255, 0 \"oooo\"", new FtpListingInfo(
                    "57-HP_NonStop",
                    528,
                    "\"oooo\"",
                    "255, 0",
                    null,
                    new DateTime(2007, 4, 6, 14, 21, 18, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "58-HP_NonStop 101 528 6-Apr-07 14:21:18 255,255 \"oooo\"", new FtpListingInfo(
                    "58-HP_NonStop",
                    528,
                    "\"oooo\"",
                    "255,255",
                    null,
                    new DateTime(2007, 4, 6, 14, 21, 18, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "drwxr-xr-x 6 user sys 1024 30. Jan., 12:40 59-localized-date-dir", new FtpListingInfo(
                    "59-localized-date-dir",
                    1024,
                    "drwxr-xr-x",
                    "user sys",
                    null,
                    new DateTime(Year(1, 30), 1, 30, 12, 40, 0, DateTimeKind.Utc),
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                "WYOSPT 3420   2003/05/21  1  200  FB      80  8053  PS  60-MVS.FILE", new FtpListingInfo(
                    "60-MVS.FILE",
                    100,
                    "",
                    "",
                    null,
                    new DateTime(2003, 5, 21, 0, 0, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "WPTA01 3290   2004/03/04  1    3  FB      80  3125  PO  61-MVS.DATASET", new FtpListingInfo(
                    "61-MVS.DATASET",
                    -1,
                    "",
                    "",
                    null,
                    new DateTime(2004, 3, 04, 0, 0, 0, DateTimeKind.Utc),
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                "NRP004 3390   **NONE**    1   15  NONE     0     0  PO  62-MVS-NONEDATE.DATASET", new FtpListingInfo(
                    "62-MVS-NONEDATE.DATASET",
                    -1,
                    "",
                    "",
                    null,
                    null,
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                "TSO005 3390   2005/06/06 213000 U 0 27998 PO 63-MVS.DATASET", new FtpListingInfo(
                    "63-MVS.DATASET",
                    -1,
                    "",
                    "",
                    null,
                    new DateTime(2005, 6, 6, 0, 0, 0, DateTimeKind.Utc),
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                "TSO004 3390   VSAM 64-mvs-file", new FtpListingInfo(
                    "64-mvs-file",
                    -1,
                    "",
                    "",
                    null,
                    null,
                    0)
            },
            new object[] {
                "65-MVS-PDS-MEMBER", new FtpListingInfo(
                    "65-MVS-PDS-MEMBER",
                    -1,
                    "",
                    "",
                    null,
                    null,
                    0)
            },
            new object[] {
                "66-MVSPDSMEMBER 01.01 2004/06/22 2004/06/22 16:32   128   128    0 BOBY12", new FtpListingInfo(
                    "66-MVSPDSMEMBER",
                    128,
                    "",
                    "",
                    null,
                    new DateTime(2004, 6, 22, 16, 32, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "67-MVSPDSMEMBER2 00B308 000411  00 FO                31    ANY", new FtpListingInfo(
                    "67-MVSPDSMEMBER2",
                    45832,
                    "",
                    "",
                    null,
                    null,
                    0)
            },
            new object[] {
                "68-MVSPDSMEMBER3 00B308 000411  00 FO        RU      ANY    24", new FtpListingInfo(
                    "68-MVSPDSMEMBER3",
                    45832,
                    "",
                    "",
                    null,
                    null,
                    0)
            },
            new object[] {
                "Migrated				69-SOME.FILE", new FtpListingInfo(
                    "69-SOME.FILE",
                    -1,
                    "",
                    "",
                    null,
                    null,
                    0)
            },
            new object[] {
                "70-ZVMFILE  TRACE   V        65      107        2 2005-10-04 15:28:42 060191", new FtpListingInfo(
                    "70-ZVMFILE.TRACE",
                    6955,
                    "",
                    "060191",
                    null,
                    new DateTime(2005, 10, 4, 15, 28, 42, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "drwxr-xr-x 3 slopri devlab 512 71-unix-dateless", new FtpListingInfo(
                    "71-unix-dateless",
                    512,
                    "drwxr-xr-x",
                    "slopri devlab",
                    null,
                    null,
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                "Type=file;mOdIfY=20081105165215;size=1234; 72-MLSD-file", new FtpListingInfo(
                    "72-MLSD-file",
                    1234,
                    "",
                    "",
                    null,
                    new DateTime(2008, 11, 5, 16, 52, 15, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "V43525 Tape                                             73-MSV-TAPE.FILE", new FtpListingInfo(
                    "73-MSV-TAPE.FILE",
                    -1,
                    "",
                    "",
                    null,
                    null,
                    0)
            },
            new object[] {
                "Type=file; 74-MLSD-whitespace trailing\t ", new FtpListingInfo(
                    "74-MLSD-whitespace trailing\t ",
                    -1,
                    "",
                    "",
                    null,
                    null,
                    0)
            },
            new object[] {
                "Type=file; \t 75-MLSD-whitespace leading", new FtpListingInfo(
                    "\t 75-MLSD-whitespace leading",
                    -1,
                    "",
                    "",
                    null,
                    null,
                    0)
            },
            new object[] {
                "modify=20080426135501;perm=;size=65718921;type=file;unique=802U1066013B;UNIX.group=1179;UNIX.mode=00;UNIX.owner=1179; 75 MLSD file with empty permissions", new FtpListingInfo(
                    "75 MLSD file with empty permissions",
                    65718921,
                    "00",
                    "1179 1179",
                    null,
                    new DateTime(2008, 4, 26, 13, 55, 1, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "type=OS.unix=slink:/foo; 76 MLSD symlink", new FtpListingInfo(
                    "76 MLSD symlink",
                    -1,
                    "",
                    "",
                    "/foo",
                    null,
                    FtpListingInfoFlags.Directory | FtpListingInfoFlags.Link)
            },
            new object[] {
                "type=OS.UNIX=symlink; 76b MLSD symlink", new FtpListingInfo(
                    "76b MLSD symlink",
                    -1,
                    "",
                    "",
                    null,
                    null,
                    FtpListingInfoFlags.Directory | FtpListingInfoFlags.Link)
            },
            new object[] {
                "type=file 77 MLSD file no trailing semicolon after facts < mlst-07", new FtpListingInfo(
                    "77 MLSD file no trailing semicolon after facts < mlst-07",
                    -1,
                    "",
                    "",
                    null,
                    null,
                    0)
            },
            new object[] {
                "type=OS.unix=slink; 77 MLSD symlink notarget", new FtpListingInfo(
                    "77 MLSD symlink notarget",
                    -1,
                    "",
                    "",
                    null,
                    null,
                    FtpListingInfoFlags.Directory | FtpListingInfoFlags.Link)
            },
            new object[] {
                "size=1365694195;type=file;modify=20090722092510;\tadsl TV 2009-07-22 08-25-10 78 mlsd file that can get parsed as unix.file", new FtpListingInfo(
                    "adsl TV 2009-07-22 08-25-10 78 mlsd file that can get parsed as unix.file",
                    1365694195,
                    "",
                    "",
                    null,
                    new DateTime(2009, 7, 22, 9, 25, 10, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "WYOSPT 3420   2003/05/21  1 ????  FB      80  8053  PS  79-MVS.FILE", new FtpListingInfo(
                    "79-MVS.FILE",
                    100,
                    "",
                    "",
                    null,
                    new DateTime(2003, 5, 21, 0, 0, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "GISBWI 3390   2011/08/25  2 ++++  FB     904 18080  PS  80-MVS.FILE", new FtpListingInfo(
                    "80-MVS.FILE",
                    100,
                    "",
                    "",
                    null,
                    new DateTime(2011, 8, 25, 0, 0, 0, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "WYOSPT 3420   2003/05/21  1 3 U 6447    6447  PO-E 81-MVS.DIR", new FtpListingInfo(
                    "81-MVS.DIR",
                    -1,
                    "",
                    "",
                    null,
                    new DateTime(2003, 5, 21, 0, 0, 0, DateTimeKind.Utc),
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                "drwxrwxrwx   1 0        0               0 29 Jul 02:27 82 2014 Invoices", new FtpListingInfo(
                    "82 2014 Invoices",
                    0,
                    "drwxrwxrwx",
                    "0 0",
                    null,
                    new DateTime(Year(7, 29), 7, 29, 2, 27, 0, DateTimeKind.Utc),
                    FtpListingInfoFlags.Directory)
            },
            new object[] {
                "Type=file;mOdIfY=19681105165215;size=1234; 83 MLSD pre-epoch", new FtpListingInfo(
                    "83 MLSD pre-epoch",
                    1234,
                    "",
                    "",
                    null,
                    new DateTime(1968, 11, 5, 16, 52, 15, DateTimeKind.Utc),
                    0)
            },
            new object[] {
                "-rw-------      1  99999999 0              3 Apr   4 24:00 84 alternate_midnight", new FtpListingInfo(
                    "84 alternate_midnight",
                    3,
                    "-rw-------",
                    "99999999 0",
                    null,
                    new DateTime(Year(4, 4), 4, 5, 0, 0, 0, DateTimeKind.Utc),
                    0)
            }
        ];

        public static IEnumerable<string> TestLines => TestCases.Select(obj => $"{((object[])obj)[0]}");

        [SetUp]
        public void SetUp() {
            Subject = null;
        }

        [TestCaseSource(nameof(TestCases))]
        public void NameIsParsed(string line, FtpListingInfo entry) {
            Compare(line, entry, e => e.Name);
        }

        [TestCaseSource(nameof(TestCases))]
        public void PermissionsAreParsed(string line, FtpListingInfo entry) {
            Compare(line, entry, e => e.Permissions);
        }

        [TestCaseSource(nameof(TestCases))]
        public void SizeIsParsed(string line, FtpListingInfo entry) {
            Compare(line, entry, e => e.Size);
        }

        [TestCaseSource(nameof(TestCases))]
        public void OwnerGroupIsParsed(string line, FtpListingInfo entry) {
            Compare(line, entry, e => e.OwnerGroup);
        }

        [TestCaseSource(nameof(TestCases))]
        public void TimeIsParsed(string line, FtpListingInfo entry) {
            Compare(line, entry, e => e.Time);
        }

        [TestCaseSource(nameof(TestCases))]
        public void FlagsAreParsed(string line, FtpListingInfo entry) {
            Compare(line, entry, e => e.Flags);
        }

        [TestCaseSource(nameof(TestCases))]
        public void TargetIsParsed(string line, FtpListingInfo entry) {
            Compare(line, entry, e => e.Target);
        }

        [Test]
        public void CorrectNumberOfEntriesAreParsedFromLines() {
            static async IAsyncEnumerable<string> contents() {
                foreach (var line in TestLines) {
                    yield return line;
                    yield return "\r\n";
                    await Task.CompletedTask;
                }
            }
            var infos = Subject.ParseContents(contents(), default).ToBlockingEnumerable().ToList();
            var actual = infos.Count;
            var expected = TestCases.Length;
            Assert.That(actual, Is.EqualTo(expected));
        }

        public static object TestLookup = TestCases.OfType<object[]>().Select((arr, i) => new object[] {
            i,
            arr[1]
        });

        [TestCaseSource(nameof(TestLookup))]
        public void NameIsParsedAtIndex(int i, FtpListingInfo info) {
            Compare(i, info, e => e.Name);
        }

        [TestCaseSource(nameof(TestLookup))]
        public void PermissionsAreParsedAtIndex(int i, FtpListingInfo info) {
            Compare(i, info, e => e.Permissions);
        }

        [TestCaseSource(nameof(TestLookup))]
        public void SizeIsParsedAtIndex(int i, FtpListingInfo info) {
            Compare(i, info, e => e.Size);
        }

        [TestCaseSource(nameof(TestLookup))]
        public void OwnerGroupIsParsedAtIndex(int i, FtpListingInfo info) {
            Compare(i, info, e => e.OwnerGroup);
        }

        [TestCaseSource(nameof(TestLookup))]
        public void TimeIsParsedAtIndex(int i, FtpListingInfo info) {
            Compare(i, info, e => e.Time);
        }

        [TestCaseSource(nameof(TestLookup))]
        public void FlagsAreParsedAtIndex(int i, FtpListingInfo info) {
            Compare(i, info, e => e.Flags);
        }

        [TestCaseSource(nameof(TestLookup))]
        public void TargetIsParsedAtIndex(int i, FtpListingInfo info) {
            Compare(i, info, e => e.Target);
        }
    }
}
