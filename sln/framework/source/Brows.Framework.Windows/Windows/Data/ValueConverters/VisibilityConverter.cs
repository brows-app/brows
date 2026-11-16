using System.Windows;

namespace Brows.Windows.Data.ValueConverters;

internal sealed class VisibilityConverter(Visibility setVisibility,
                                          Visibility unsetVisibility,
                                          bool zeroIsUnset = false)
: ValueConverter {
    private readonly Visibility SetVisibility = setVisibility;
    private readonly Visibility UnsetVisibility = unsetVisibility;
    private readonly bool ZeroIsUnset = zeroIsUnset;

    private protected sealed override object Convert(object value, Type targetType,
                                                     object parameter, CultureInfo culture) {
        var unset =
            value is null ||
            value is false ||
            object.Equals(value, null) ||
            object.Equals(value, false) ||
            object.Equals(value, string.Empty) ||
            (ZeroIsUnset &&
                (value is 0 || object.Equals(value, 0)));
        if (unset) {
            return UnsetVisibility;
        }
        return SetVisibility;
    }
}
