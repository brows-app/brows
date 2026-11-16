using System.Windows.Data;

namespace Brows.Windows.Data.ValueConverters;

[ValueConversion(typeof(object), typeof(string))]
internal sealed class ToLowerConverter : ValueConverter {
    private protected sealed override object Convert(object value,
                                                     Type targetType,
                                                     object parameter,
                                                     CultureInfo culture) {
        bool isValidTarget = targetType is null ||
                             targetType == typeof(string) ||
                             targetType == typeof(object);
        if (isValidTarget) {
            return value?.ToString()?.ToLower(culture);
        }
        return base.Convert(value, targetType, parameter, culture);
    }
}
