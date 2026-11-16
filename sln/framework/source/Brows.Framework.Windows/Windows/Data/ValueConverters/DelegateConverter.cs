using System;

namespace Brows.Windows.Data.ValueConverters;

internal sealed class DelegateConverter : ValueConverter {
    public Func<object, Type, object, CultureInfo, object> OnConvert { get; set; }
    public Func<object, Type, object, CultureInfo, object> OnConvertBack { get; set; }

    private protected sealed override object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        var onConvert = OnConvert;
        if (onConvert is not null) {
            return onConvert(value, targetType, parameter, culture);
        }
        return base.Convert(value, targetType, parameter, culture);
    }

    private protected sealed override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        var onConvertBack = OnConvertBack;
        if (onConvertBack is not null) {
            return onConvertBack(value, targetType, parameter, culture);
        }
        return base.ConvertBack(value, targetType, parameter, culture);
    }
}
