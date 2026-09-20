using System.Globalization;

namespace Brows.Entries;

public interface IEntryDataConverter {
    string Convert(object value, object parameter, CultureInfo culture);
}
