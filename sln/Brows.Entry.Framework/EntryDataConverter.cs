using System;
using System.Globalization;
using CONVERT = System.Convert;

namespace Brows {
    using IO;
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

        private class DateTimeConverter : EntryDataConverter {
            public string Format { get; set; } = "yyyy-MM-dd HH:mm:ss";

            public override string Convert(object value, object parameter, CultureInfo culture) {
                if (value == null) return null;

                var converted = Try(CONVERT.ToDateTime, value, out var dateTime);
                if (converted == false) return null;

                var format = parameter?.ToString() ?? Format;
                var formatted = dateTime.ToString(format, culture);
                return formatted;
            }
        }

        private class BooleanYesNoConverter : EntryDataConverter {
            public override string Convert(object value, object parameter, CultureInfo culture) {
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
