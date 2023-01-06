using Domore.Converting;
using System;
using System.Globalization;
using CONVERT = System.Convert;

namespace Brows {
    using Translation;

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

        private sealed class FileSystemSizeConverter : EntryDataConverter {
            public string Format { get; set; } = "0.#";

            public sealed override string Convert(object value, object parameter, CultureInfo culture) {
                if (value == null) return null;

                var converted = Try(CONVERT.ToInt64, value, out var length);
                if (converted == false) return null;

                var format = parameter?.ToString() ?? Format;
                var readable = FileSize.From(length, format, culture);
                return readable;
            }
        }


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
                var s = Global.Translation.Value(key);
                return s;
            }
        }
    }
}
