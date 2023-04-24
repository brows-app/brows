using System;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace Brows.Windows.Data {
    internal class NullFallbackConverter : ValueConverter {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value == null
                ? DependencyProperty.UnsetValue
                : parameter;
        }

        public override object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            return values.All(i => i == null)
                ? DependencyProperty.UnsetValue
                : parameter;
        }
    }
}
