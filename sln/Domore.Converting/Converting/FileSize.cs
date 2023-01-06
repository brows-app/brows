using System;

namespace Domore.Converting {
    public static class FileSize {
        public static string From(long fileLength, string format, IFormatProvider formatProvider) {
            string unit;
            double read;
            var abs = (fileLength < 0 ? -fileLength : fileLength);
            if (abs >= 0x1000000000000000) {
                unit = "EiB";
                read = (fileLength >> 50);
            }
            else if (abs >= 0x4000000000000) {
                unit = "PiB";
                read = (fileLength >> 40);
            }
            else if (abs >= 0x10000000000) {
                unit = "TiB";
                read = (fileLength >> 30);
            }
            else if (abs >= 0x40000000) {
                unit = "GiB";
                read = (fileLength >> 20);
            }
            else if (abs >= 0x100000) {
                unit = "MiB";
                read = (fileLength >> 10);
            }
            else if (abs >= 0x400) {
                unit = "KiB";
                read = fileLength;
            }
            else {
                return fileLength.ToString("0 B");
            }
            read = (read / 1024);
            return read.ToString(format, formatProvider) + " " + unit;
        }
    }
}
