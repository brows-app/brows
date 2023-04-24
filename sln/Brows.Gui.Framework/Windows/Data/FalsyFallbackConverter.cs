using System;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace Brows.Windows.Data {
    internal class FalsyFallbackConverter : ValueConverter {
        private static readonly object[] Zeroes = new object[] {
            null,
            "",
            '\0',
            false,
            (byte)0,
            (sbyte)0,
            (short)0,
            (ushort)0,
            0,
            (uint)0,
            (long)0,
            (ulong)0,
            (float)0,
            float.NaN,
            (double)0,
            double.NaN
        };

        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return Zeroes.Any(zero => Equals(zero, value))
                ? DependencyProperty.UnsetValue
                : parameter;
        }

        public override object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            return values.All(value => Zeroes.Any(zero => Equals(zero, value)))
                ? DependencyProperty.UnsetValue
                : parameter;
        }
    }
}
