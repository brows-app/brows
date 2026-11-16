using System.Windows;

namespace Brows.Windows.Data.ValueConverters;

internal abstract class FallbackConverter : ValueConverter {
    private protected abstract bool UnsetCondition(object value);

    private protected sealed override object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        var unset = UnsetCondition(value);
        if (unset) {
            return DependencyProperty.UnsetValue;
        }
        return parameter;
    }

    internal sealed class Default : FallbackConverter {
        private protected sealed override bool UnsetCondition(object value) {
            return value is null ||
                   value is false ||
                   object.Equals(value, null) ||
                   object.Equals(value, false);
        }
    }

    internal sealed class Falsy : FallbackConverter {
        private protected sealed override bool UnsetCondition(object value) {
            return value switch {
                null or
                false or
                default(char) or
                default(byte) or
                default(sbyte) or
                default(int) or
                default(uint) or
                default(nint) or
                default(nuint) or
                default(long) or
                default(ulong) or
                default(float) or
                default(double) or
                default(decimal) or
                "" => true,
                _ => false
            };
        }
    }

    internal sealed class IfNull : FallbackConverter {
        private protected sealed override bool UnsetCondition(object value) {
            return value is null || value == null || value.Equals(null);
        }
    }

    internal sealed class IfNullOrWhitespace : FallbackConverter {
        private protected sealed override bool UnsetCondition(object value) {
            return value is null || value == null || value.Equals(null) ||
                   value is string s && string.IsNullOrWhiteSpace(s);
        }
    }

    internal sealed class IfFalse : FallbackConverter {
        private protected sealed override bool UnsetCondition(object value) {
            return value is false || (value?.Equals(false) == true);
        }
    }

    internal sealed class IfZero : FallbackConverter {
        private protected sealed override bool UnsetCondition(object value) {
            return value switch {
                default(char) or
                default(byte) or
                default(sbyte) or
                default(int) or
                default(uint) or
                default(nint) or
                default(nuint) or
                default(long) or
                default(ulong) or
                default(float) or
                default(double) or
                default(decimal)
                    => true,
                _ => false
            };
        }
    }
}
