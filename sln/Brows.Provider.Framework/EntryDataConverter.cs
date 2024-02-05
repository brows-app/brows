using System;
using System.Globalization;
using CONVERT = System.Convert;

namespace Brows {
    public abstract class EntryDataConverter : IEntryDataConverter {
        protected bool Try<T>(Func<object, T> conversion, object input, out T result) {
            try {
                result = conversion(input);
                return true;
            }
            catch {
                result = default;
                return false;
            }
        }

        public abstract string Convert(object value, object parameter, CultureInfo culture);

        public static EntryDataConverter DateTime { get; } = new DateTimeConverter();
        public static EntryDataConverter BooleanYesNo { get; } = new BooleanYesNoConverter();
        public static EntryDataConverter FileSystemSize { get; } = new FileSystemSizeConverter();

        private sealed class DateTimeConverter : EntryDataConverter {
            public string Format { get; set; } = "yyyy-MM-dd HH:mm:ss";

            public sealed override string Convert(object value, object parameter, CultureInfo culture) {
                if (value == null) return null;

                var converted = Try(CONVERT.ToDateTime, value, out var dateTime);
                if (converted == false) return null;

                var format = parameter?.ToString() ?? Format;
                var formatted = dateTime.ToString(format, culture);
                return formatted;
            }
        }

        private sealed class BooleanYesNoConverter : EntryDataConverter {
            public sealed override string Convert(object value, object parameter, CultureInfo culture) {
                if (value == null) return null;

                var converted = Try(CONVERT.ToBoolean, value, out var boolean);
                if (converted == false) return null;

                var yesNo = boolean ? "Yes" : "No";
                var key = $"EntryData_{yesNo}";
                var s = Translation.Global.Value(key);
                return s;
            }
        }

        private sealed class FileSystemSizeConverter : EntryDataConverter {
            public sealed override string Convert(object value, object parameter, CultureInfo culture) {
                if (value == null) return null;

                var converted = Try(CONVERT.ToInt64, value, out var length);
                if (converted == false) return null;

                var format = parameter?.ToString();
                var readable = FileSize.From(length, format, culture);
                return readable;
            }
        }

        internal static class FileSize {
            public static string From(long fileLength, string format, IFormatProvider formatProvider) {
                string frmt = string.IsNullOrWhiteSpace(format) ? null : format;
                string unit;
                double size;
                var abs = (fileLength < 0 ? -fileLength : fileLength);
                switch (abs) {
                    case >= 0x1000000000000000:
                        unit = "EiB";
                        size = (fileLength >> 50);
                        frmt = frmt ?? "0.##";
                        break;
                    case >= 0x4000000000000:
                        unit = "PiB";
                        size = (fileLength >> 40);
                        frmt = frmt ?? "0.##";
                        break;
                    case >= 0x10000000000:
                        unit = "TiB";
                        size = (fileLength >> 30);
                        frmt = frmt ?? "0.##";
                        break;
                    case >= 0x40000000:
                        unit = "GiB";
                        size = (fileLength >> 20);
                        frmt = frmt ?? "0.##";
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
}
