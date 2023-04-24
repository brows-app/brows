using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Brows.Windows.Data {
    public class ValueConverter : IValueConverter, IMultiValueConverter {
        public static ValueConverter Bool { get; } = new BoolValueConverter();
        public static ValueConverter ToUpper { get; } = new ToUpperValueConverter();
        public static ValueConverter ToLower { get; } = new ToLowerValueConverter();
        public static ValueConverter Resource { get; } = new ResourceValueConverter();
        public static ValueConverter NullFallback { get; } = new NullFallbackConverter();
        public static ValueConverter FalseFallback { get; } = new FalseFallbackConverter();
        public static ValueConverter FalsyFallback { get; } = new FalsyFallbackConverter();
        public static ValueConverter AssemblyVersion { get; } = new AssemblyVersionValueConverter();

        public virtual object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }

        public virtual object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }

        public virtual object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }

        public virtual object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }

        private class BoolValueConverter : ValueConverter {
            public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                var boolValue = value as bool? ?? false;
                return boolValue == false
                    ? DependencyProperty.UnsetValue
                    : parameter;
            }
        }

        private class ToUpperValueConverter : ValueConverter {
            public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                if (value != null) {
                    var s = value.ToString();
                    if (s != null) {
                        return s.ToUpper(culture);
                    }
                }
                return null;
            }
        }

        private class ToLowerValueConverter : ValueConverter {
            public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                if (value != null) {
                    var s = value.ToString();
                    if (s != null) {
                        return s.ToLower(culture);
                    }
                }
                return null;
            }
        }

        private class ResourceValueConverter : ValueConverter {
        }

        private class AssemblyVersionValueConverter : ValueConverter {
            public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                return value == null
                    ? base.Convert(value, targetType, parameter, culture)
                    : value.GetType().Assembly.GetName().Version.ToString();
            }
        }
    }
}
