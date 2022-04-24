using System;
using System.Globalization;
using System.Windows.Data;

namespace Brows.Windows.Data {
    public class EntryValueConverter : IValueConverter {
        private IEntryDataConverter Agent { get; }

        private EntryValueConverter(IEntryDataConverter agent) {
            Agent = agent ?? throw new ArgumentNullException(nameof(agent));
        }

        public static IValueConverter FileSystemSize { get; } = new EntryValueConverter(EntryDataConverter.FileSystemSize);

        public static IValueConverter From(IEntryDataConverter converter) {
            return new EntryValueConverter(converter);
        }

        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return Agent.Convert(value, parameter, culture);
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
