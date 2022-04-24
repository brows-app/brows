using System;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace Brows.Windows.Data {
    internal class FalseFallbackConverter : ValueConverter {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return false.Equals(value)
                ? DependencyProperty.UnsetValue
                : parameter;
        }

        public override object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            return values.All(value => false.Equals(value))
                ? DependencyProperty.UnsetValue
                : parameter;
        }
    }
}
