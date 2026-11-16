using System;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace Brows.Windows.Data.MultiValueConverters;

internal sealed class ValuesEqualConverter : MultiValueConverter {
    protected sealed override object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
        var equals = values?.Distinct()?.Count() == 1;
        if (equals) {
            return parameter;
        }
        return DependencyProperty.UnsetValue;
    }
}
