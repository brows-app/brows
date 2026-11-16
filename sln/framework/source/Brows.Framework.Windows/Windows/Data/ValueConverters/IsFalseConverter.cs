namespace Brows.Windows.Data.ValueConverters;

internal sealed class IsFalseConverter : ValueConverter {
    private protected sealed override object Convert(object value,
                                                     Type targetType,
                                                     object parameter,
                                                     CultureInfo culture) {
        return (value is false) || (value?.Equals(false) == true);
    }
}
