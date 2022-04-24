using System.Globalization;

namespace Brows {
    using IO;

    public abstract class EntryDataConverter : IEntryDataConverter {
        public abstract string Convert(object value, object parameter, CultureInfo culture);

        public static EntryDataConverter FileSystemSize { get; } = new FileSystemSizeConverter();
    }
}
