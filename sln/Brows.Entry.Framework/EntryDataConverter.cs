using System.Globalization;

namespace Brows {
    using IO;
    using Translation;
    using CONVERT = System.Convert;

    public abstract class EntryDataConverter : IEntryDataConverter {
        public abstract string Convert(object value, object parameter, CultureInfo culture);

        public static EntryDataConverter DateTime { get; } = new DateTimeConverter();
        public static EntryDataConverter BooleanYesNo { get; } = new BooleanYesNoConverter();
        public static EntryDataConverter FileSystemSize { get; } = new FileSystemSizeConverter();

        private class DateTimeConverter : EntryDataConverter {
            public string Format { get; set; } = "yyyy-MM-dd HH:mm:ss";

            public override string Convert(object value, object parameter, CultureInfo culture) {
                if (value == null) return null;
                var format = parameter?.ToString() ?? Format;
                var dateTime = CONVERT.ToDateTime(value);
                var formatted = dateTime.ToString(format, culture);
                return formatted;
            }
        }

        private class BooleanYesNoConverter : EntryDataConverter {
            public override string Convert(object value, object parameter, CultureInfo culture) {
                if (value == null) return null;
                var boolean = CONVERT.ToBoolean(value);
                var yesNo = boolean ? "Yes" : "No";
                var key = $"EntryData_{yesNo}";
                var s = Global.Translation.Value(key);
                return s;
            }
        }
    }
}
