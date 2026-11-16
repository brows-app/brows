using System;

using DATETIME = System.DateTime;

namespace Brows.Windows.Data.ValueConverters;

internal sealed class DateTimeConverter : ValueConverter {
    private protected sealed override object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value is not DATETIME dt) {
            return base.Convert(value, targetType, parameter, culture);
        }
        if (dt.Kind == DateTimeKind.Unspecified) {
            dt = DATETIME.SpecifyKind(dt, DateTimeKind.Utc);
        }
        if (dt.Kind == DateTimeKind.Utc) {
            dt = dt.ToLocalTime();
        }
        return parameter is string format
            ? dt.ToString(format, culture)
            : dt.ToString(culture);
    }
}
