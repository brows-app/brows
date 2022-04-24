using System.Globalization;
using CONVERT = System.Convert;

namespace Brows.IO {
    internal class FileSystemSizeConverter : EntryDataConverter {
        public override string Convert(object value, object parameter, CultureInfo culture) {
            if (value == null) return null;
            var format = parameter?.ToString() ?? "0.#";
            var length = CONVERT.ToInt64(value);
            var readable = ToReadable(length, format);
            return readable;
        }

        public static string ToReadable(long length, string format) {
            string unit;
            double read;

            var abs = (length < 0 ? -length : length);
            if (abs >= 0x1000000000000000) {
                unit = "EiB";
                read = (length >> 50);
            }
            else if (abs >= 0x4000000000000) {
                unit = "PiB";
                read = (length >> 40);
            }
            else if (abs >= 0x10000000000) {
                unit = "TiB";
                read = (length >> 30);
            }
            else if (abs >= 0x40000000) {
                unit = "GiB";
                read = (length >> 20);
            }
            else if (abs >= 0x100000) {
                unit = "MiB";
                read = (length >> 10);
            }
            else if (abs >= 0x400) {
                unit = "KiB";
                read = length;
            }
            else {
                return length.ToString("0 B");
            }
            read = (read / 1024);
            return read.ToString($"{format} ") + unit;
        }
    }
}
