using System.Globalization;

namespace Brows {
    public interface IEntryDataConverter {
        string Convert(object value, object parameter, CultureInfo culture);
    }
}
