using System;
using System.Globalization;
using System.Windows.Data;

namespace Brows.Windows.Data;

internal abstract class MultiValueConverter : IMultiValueConverter {
    protected virtual object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
        throw new NotSupportedException();
    }

    protected virtual object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
        throw new NotSupportedException();
    }

    object IMultiValueConverter.Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
        return Convert(values, targetType, parameter, culture);
    }

    object[] IMultiValueConverter.ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
        return ConvertBack(value, targetTypes, parameter, culture);
    }
}
