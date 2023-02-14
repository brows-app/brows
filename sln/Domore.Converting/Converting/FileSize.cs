using System;

namespace Domore.Converting {
    public static class FileSize {
        public static string From(long fileLength, string format, IFormatProvider formatProvider) {
            string frmt = string.IsNullOrWhiteSpace(format) ? null : format;
            string unit;
            double size;
            var abs = (fileLength < 0 ? -fileLength : fileLength);
            switch (abs) {
                case >= 0x1000000000000000:
                    unit = "EiB";
                    size = (fileLength >> 50);
                    frmt = frmt ?? "0.######";
                    break;
                case >= 0x4000000000000:
                    unit = "PiB";
                    size = (fileLength >> 40);
                    frmt = frmt ?? "0.#####";
                    break;
                case >= 0x10000000000:
                    unit = "TiB";
                    size = (fileLength >> 30);
                    frmt = frmt ?? "0.####";
                    break;
                case >= 0x40000000:
                    unit = "GiB";
                    size = (fileLength >> 20);
                    frmt = frmt ?? "0.###";
                    break;
                case >= 0x100000:
                    unit = "MiB";
                    size = (fileLength >> 10);
                    frmt = frmt ?? "0.##";
                    break;
                case >= 0x400:
                    unit = "KiB";
                    size = fileLength;
                    frmt = frmt ?? "0.#";
                    break;
                default:
                    return fileLength.ToString("0 B");
            }
            size = (size / 1024);
            return size.ToString(frmt, formatProvider) + " " + unit;
        }
    }
}
